using TineyTo.Functions.Services;

namespace TineyTo.Functions.Tests.Mocks;

/// <summary>
/// Mock rate limiter for testing that allows all requests by default.
/// Can be configured to block specific keys.
/// </summary>
public class MockRateLimiter : IRateLimiter
{
    private readonly Dictionary<string, RateLimitResult> _blockedKeys = new();
    
    /// <summary>
    /// Gets the count of calls to each rate limit method.
    /// </summary>
    public Dictionary<string, int> CallCounts { get; } = new();

    /// <summary>
    /// Configures the rate limiter to block a specific key.
    /// </summary>
    public void BlockKey(string prefix, string key, int count = 100, int limit = 10, int retryAfter = 60)
    {
        _blockedKeys[$"{prefix}{key}"] = RateLimitResult.Blocked($"{prefix}{key}", count, limit, retryAfter);
    }

    /// <summary>
    /// Clears all blocked keys.
    /// </summary>
    public void Reset()
    {
        _blockedKeys.Clear();
        CallCounts.Clear();
    }

    public RateLimitResult CheckShortenUrl(string url)
    {
        IncrementCallCount("ShortenUrl");
        return GetResult("rl:shorten:url:", url);
    }

    public RateLimitResult CheckShortenIp(string ip)
    {
        IncrementCallCount("ShortenIp");
        return GetResult("rl:shorten:ip:", ip);
    }

    public RateLimitResult CheckRedirectAlias(string alias)
    {
        IncrementCallCount("RedirectAlias");
        return GetResult("rl:redirect:alias:", alias);
    }

    public RateLimitResult CheckRedirectIp(string ip)
    {
        IncrementCallCount("RedirectIp");
        return GetResult("rl:redirect:ip:", ip);
    }

    public RateLimitResult RecordNotFound(string ip)
    {
        IncrementCallCount("NotFound");
        return GetResult("rl:404:ip:", ip);
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

    private RateLimitResult GetResult(string prefix, string key)
    {
        var fullKey = $"{prefix}{key}";
        if (_blockedKeys.TryGetValue(fullKey, out var result))
        {
            return result;
        }
        return RateLimitResult.Allowed(fullKey, 1, 100);
    }

    private void IncrementCallCount(string method)
    {
        if (!CallCounts.ContainsKey(method))
        {
            CallCounts[method] = 0;
        }
        CallCounts[method]++;
    }
}
