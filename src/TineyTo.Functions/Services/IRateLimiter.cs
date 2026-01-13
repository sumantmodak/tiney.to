namespace TineyTo.Functions.Services;

/// <summary>
/// Result of a rate limit check.
/// </summary>
public readonly struct RateLimitResult
{
    /// <summary>
    /// Whether the request is allowed.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Current count within the window.
    /// </summary>
    public int CurrentCount { get; init; }

    /// <summary>
    /// Maximum allowed count within the window.
    /// </summary>
    public int Limit { get; init; }

    /// <summary>
    /// Seconds until the window resets.
    /// </summary>
    public int RetryAfterSeconds { get; init; }

    /// <summary>
    /// The key that was rate limited.
    /// </summary>
    public string Key { get; init; }

    public static RateLimitResult Allowed(string key, int count, int limit) => new()
    {
        IsAllowed = true,
        CurrentCount = count,
        Limit = limit,
        RetryAfterSeconds = 0,
        Key = key
    };

    public static RateLimitResult Blocked(string key, int count, int limit, int retryAfter) => new()
    {
        IsAllowed = false,
        CurrentCount = count,
        Limit = limit,
        RetryAfterSeconds = retryAfter,
        Key = key
    };
}

/// <summary>
/// Interface for rate limiting operations.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Checks if a shorten request for a URL is allowed.
    /// </summary>
    RateLimitResult CheckShortenUrl(string url);

    /// <summary>
    /// Checks if a shorten request from an IP is allowed.
    /// </summary>
    RateLimitResult CheckShortenIp(string ip);

    /// <summary>
    /// Checks if a redirect request for an alias is allowed.
    /// </summary>
    RateLimitResult CheckRedirectAlias(string alias);

    /// <summary>
    /// Checks if a redirect request from an IP is allowed.
    /// </summary>
    RateLimitResult CheckRedirectIp(string ip);

    /// <summary>
    /// Records a 404 (not found) for an IP and checks if blocked.
    /// </summary>
    RateLimitResult RecordNotFound(string ip);

    /// <summary>
    /// Checks multiple rate limits at once. Returns first blocked result, or last allowed result.
    /// </summary>
    RateLimitResult CheckAll(params Func<RateLimitResult>[] checks);
}
