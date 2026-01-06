using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TineyTo.Functions.Configuration;

namespace TineyTo.Functions.Services;

/// <summary>
/// In-memory rate limiter using sliding window counters.
/// Uses IMemoryCache for automatic expiration and memory management.
/// Thread-safe using ConcurrentDictionary for counters within cache entries.
/// </summary>
public class SlidingWindowRateLimiter : IRateLimiter
{
    private readonly IMemoryCache _cache;
    private readonly RateLimitConfiguration _config;
    private readonly ITimeProvider _timeProvider;
    private readonly ILogger<SlidingWindowRateLimiter> _logger;

    // Cache key prefixes for different rate limit buckets
    private const string PrefixShortenUrl = "rl:shorten:url:";
    private const string PrefixShortenIp = "rl:shorten:ip:";
    private const string PrefixRedirectAlias = "rl:redirect:alias:";
    private const string PrefixRedirectIp = "rl:redirect:ip:";
    private const string PrefixNotFoundIp = "rl:404:ip:";

    public SlidingWindowRateLimiter(
        IMemoryCache cache,
        RateLimitConfiguration config,
        ITimeProvider timeProvider,
        ILogger<SlidingWindowRateLimiter> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public RateLimitResult CheckShortenUrl(string url)
    {
        if (!_config.IsEnabled)
            return RateLimitResult.Allowed($"{PrefixShortenUrl}{url}", 0, _config.ShortenPerUrlLimit);

        // Use hash to keep keys short
        var urlHash = GetStableHash(url);
        return CheckLimit(
            $"{PrefixShortenUrl}{urlHash}",
            _config.ShortenPerUrlLimit,
            _config.ShortenPerUrlWindowSeconds);
    }

    public RateLimitResult CheckShortenIp(string ip)
    {
        if (!_config.IsEnabled)
            return RateLimitResult.Allowed($"{PrefixShortenIp}{ip}", 0, _config.ShortenPerIpLimit);

        return CheckLimit(
            $"{PrefixShortenIp}{ip}",
            _config.ShortenPerIpLimit,
            _config.ShortenPerIpWindowSeconds);
    }

    public RateLimitResult CheckRedirectAlias(string alias)
    {
        if (!_config.IsEnabled)
            return RateLimitResult.Allowed($"{PrefixRedirectAlias}{alias}", 0, _config.RedirectPerAliasLimit);

        return CheckLimit(
            $"{PrefixRedirectAlias}{alias}",
            _config.RedirectPerAliasLimit,
            _config.RedirectPerAliasWindowSeconds);
    }

    public RateLimitResult CheckRedirectIp(string ip)
    {
        if (!_config.IsEnabled)
            return RateLimitResult.Allowed($"{PrefixRedirectIp}{ip}", 0, _config.RedirectPerIpLimit);

        return CheckLimit(
            $"{PrefixRedirectIp}{ip}",
            _config.RedirectPerIpLimit,
            _config.RedirectPerIpWindowSeconds);
    }

    public RateLimitResult RecordNotFound(string ip)
    {
        if (!_config.IsEnabled)
            return RateLimitResult.Allowed($"{PrefixNotFoundIp}{ip}", 0, _config.NotFoundPerIpLimit);

        return CheckLimit(
            $"{PrefixNotFoundIp}{ip}",
            _config.NotFoundPerIpLimit,
            _config.NotFoundPerIpWindowSeconds);
    }

    public RateLimitResult CheckAll(params Func<RateLimitResult>[] checks)
    {
        RateLimitResult lastResult = default;
        
        foreach (var check in checks)
        {
            lastResult = check();
            if (!lastResult.IsAllowed)
            {
                return lastResult;
            }
        }
        
        return lastResult;
    }

    /// <summary>
    /// Core rate limiting logic using sliding window with fixed buckets.
    /// Uses a simple counter that gets incremented and cached with expiration.
    /// </summary>
    private RateLimitResult CheckLimit(string key, int limit, int windowSeconds)
    {
        var now = _timeProvider.UtcNow;
        var windowStart = now.ToUnixTimeSeconds() / windowSeconds * windowSeconds;
        var windowKey = $"{key}:{windowStart}";

        // Get or create counter for this window
        var counter = _cache.GetOrCreate(windowKey, entry =>
        {
            // Set expiration to window size + buffer for late arrivals
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(windowSeconds * 2);
            entry.Size = 1; // Count as 1 unit for cache size limiting
            return new Counter();
        })!;

        var count = counter.Increment();
        var retryAfter = (int)(windowStart + windowSeconds - now.ToUnixTimeSeconds());

        if (count > limit)
        {
            _logger.LogWarning(
                "Rate limit exceeded for {Key}: {Count}/{Limit} (retry after {RetryAfter}s)",
                key, count, limit, retryAfter);

            return RateLimitResult.Blocked(key, count, limit, retryAfter);
        }

        return RateLimitResult.Allowed(key, count, limit);
    }

    /// <summary>
    /// Creates a stable hash for long strings (like URLs) to keep cache keys short.
    /// </summary>
    private static string GetStableHash(string input)
    {
        // Simple stable hash - not cryptographic, just for bucketing
        unchecked
        {
            int hash = 17;
            foreach (char c in input)
            {
                hash = hash * 31 + c;
            }
            return hash.ToString("x8");
        }
    }

    /// <summary>
    /// Thread-safe counter for rate limiting.
    /// </summary>
    private class Counter
    {
        private int _value;

        public int Increment() => Interlocked.Increment(ref _value);
        public int Value => _value;
    }
}

/// <summary>
/// No-op rate limiter for testing or when rate limiting is disabled.
/// </summary>
public class NoOpRateLimiter : IRateLimiter
{
    public RateLimitResult CheckShortenUrl(string url) => 
        RateLimitResult.Allowed("noop", 0, int.MaxValue);

    public RateLimitResult CheckShortenIp(string ip) => 
        RateLimitResult.Allowed("noop", 0, int.MaxValue);

    public RateLimitResult CheckRedirectAlias(string alias) => 
        RateLimitResult.Allowed("noop", 0, int.MaxValue);

    public RateLimitResult CheckRedirectIp(string ip) => 
        RateLimitResult.Allowed("noop", 0, int.MaxValue);

    public RateLimitResult RecordNotFound(string ip) => 
        RateLimitResult.Allowed("noop", 0, int.MaxValue);

    public RateLimitResult CheckAll(params Func<RateLimitResult>[] checks) => 
        RateLimitResult.Allowed("noop", 0, int.MaxValue);
}
