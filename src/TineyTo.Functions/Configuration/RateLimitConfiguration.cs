namespace TineyTo.Functions.Configuration;

/// <summary>
/// Configuration for rate limiting to protect against abuse.
/// Uses sliding window counters with configurable thresholds.
/// </summary>
public class RateLimitConfiguration
{
    /// <summary>
    /// Whether rate limiting is enabled. Default is true.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    // ==================== SHORTEN LIMITS ====================

    /// <summary>
    /// Max shorten requests per URL within the time window.
    /// Prevents repeated shortening of the same URL.
    /// Default: 5 requests per window.
    /// </summary>
    public int ShortenPerUrlLimit { get; init; } = 5;

    /// <summary>
    /// Time window in seconds for per-URL shorten rate limit.
    /// Default: 60 seconds.
    /// </summary>
    public int ShortenPerUrlWindowSeconds { get; init; } = 60;

    /// <summary>
    /// Max shorten requests per IP within the time window.
    /// Prevents spam from a single IP.
    /// Default: 10 requests per window.
    /// </summary>
    public int ShortenPerIpLimit { get; init; } = 10;

    /// <summary>
    /// Time window in seconds for per-IP shorten rate limit.
    /// Default: 60 seconds.
    /// </summary>
    public int ShortenPerIpWindowSeconds { get; init; } = 60;

    // ==================== REDIRECT LIMITS ====================

    /// <summary>
    /// Max redirect requests per alias within the time window.
    /// Prevents hotlinking abuse on popular links.
    /// Default: 100 requests per window.
    /// </summary>
    public int RedirectPerAliasLimit { get; init; } = 100;

    /// <summary>
    /// Time window in seconds for per-alias redirect rate limit.
    /// Default: 10 seconds.
    /// </summary>
    public int RedirectPerAliasWindowSeconds { get; init; } = 10;

    /// <summary>
    /// Max redirect requests per IP within the time window.
    /// Prevents scraping and abuse.
    /// Default: 60 requests per window.
    /// </summary>
    public int RedirectPerIpLimit { get; init; } = 60;

    /// <summary>
    /// Time window in seconds for per-IP redirect rate limit.
    /// Default: 60 seconds.
    /// </summary>
    public int RedirectPerIpWindowSeconds { get; init; } = 60;

    // ==================== PROTECTION LIMITS ====================

    /// <summary>
    /// Max 404 (not found) responses per IP within the time window.
    /// Protects against alias scanning attacks.
    /// Default: 20 per window.
    /// </summary>
    public int NotFoundPerIpLimit { get; init; } = 20;

    /// <summary>
    /// Time window in seconds for 404 rate limit.
    /// Default: 60 seconds.
    /// </summary>
    public int NotFoundPerIpWindowSeconds { get; init; } = 60;

    /// <summary>
    /// Loads rate limit configuration from environment variables with fallback to defaults.
    /// </summary>
    public static RateLimitConfiguration LoadFromEnvironment()
    {
        return new RateLimitConfiguration
        {
            IsEnabled = !bool.TryParse(
                Environment.GetEnvironmentVariable("RATE_LIMIT_ENABLED"),
                out var enabled) || enabled,

            // Shorten limits
            ShortenPerUrlLimit = int.TryParse(
                Environment.GetEnvironmentVariable("RATE_LIMIT_SHORTEN_PER_URL"),
                out var shortenPerUrl) ? shortenPerUrl : 5,

            ShortenPerUrlWindowSeconds = int.TryParse(
                Environment.GetEnvironmentVariable("RATE_LIMIT_SHORTEN_PER_URL_WINDOW"),
                out var shortenPerUrlWindow) ? shortenPerUrlWindow : 60,

            ShortenPerIpLimit = int.TryParse(
                Environment.GetEnvironmentVariable("RATE_LIMIT_SHORTEN_PER_IP"),
                out var shortenPerIp) ? shortenPerIp : 10,

            ShortenPerIpWindowSeconds = int.TryParse(
                Environment.GetEnvironmentVariable("RATE_LIMIT_SHORTEN_PER_IP_WINDOW"),
                out var shortenPerIpWindow) ? shortenPerIpWindow : 60,

            // Redirect limits
            RedirectPerAliasLimit = int.TryParse(
                Environment.GetEnvironmentVariable("RATE_LIMIT_REDIRECT_PER_ALIAS"),
                out var redirectPerAlias) ? redirectPerAlias : 100,

            RedirectPerAliasWindowSeconds = int.TryParse(
                Environment.GetEnvironmentVariable("RATE_LIMIT_REDIRECT_PER_ALIAS_WINDOW"),
                out var redirectPerAliasWindow) ? redirectPerAliasWindow : 10,

            RedirectPerIpLimit = int.TryParse(
                Environment.GetEnvironmentVariable("RATE_LIMIT_REDIRECT_PER_IP"),
                out var redirectPerIp) ? redirectPerIp : 60,

            RedirectPerIpWindowSeconds = int.TryParse(
                Environment.GetEnvironmentVariable("RATE_LIMIT_REDIRECT_PER_IP_WINDOW"),
                out var redirectPerIpWindow) ? redirectPerIpWindow : 60,

            // Protection limits
            NotFoundPerIpLimit = int.TryParse(
                Environment.GetEnvironmentVariable("RATE_LIMIT_404_PER_IP"),
                out var notFoundPerIp) ? notFoundPerIp : 20,

            NotFoundPerIpWindowSeconds = int.TryParse(
                Environment.GetEnvironmentVariable("RATE_LIMIT_404_PER_IP_WINDOW"),
                out var notFoundPerIpWindow) ? notFoundPerIpWindow : 60,
        };
    }
}
