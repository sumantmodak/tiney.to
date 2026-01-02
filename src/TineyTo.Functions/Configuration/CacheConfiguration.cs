namespace TineyTo.Functions.Configuration;

/// <summary>
/// Configuration for the in-memory cache used for URL shortener.
/// </summary>
public class CacheConfiguration
{
    /// <summary>
    /// Whether caching is enabled. Default is true.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Maximum cache size in megabytes. Default is 50 MB (~50k URLs).
    /// </summary>
    public int SizeLimitMb { get; init; }

    /// <summary>
    /// Default cache TTL in seconds for successful lookups. Default is 300 seconds (5 minutes).
    /// </summary>
    public int DefaultTtlSeconds { get; init; }

    /// <summary>
    /// Cache TTL in seconds for negative (404) lookups. Default is 60 seconds (1 minute).
    /// </summary>
    public int NegativeTtlSeconds { get; init; }

    /// <summary>
    /// Creates a CacheConfiguration with default values.
    /// </summary>
    public CacheConfiguration()
    {
        IsEnabled = true;
        SizeLimitMb = 50;
        DefaultTtlSeconds = 300;
        NegativeTtlSeconds = 60;
    }

    /// <summary>
    /// Loads cache configuration from environment variables with fallback to defaults.
    /// </summary>
    public static CacheConfiguration LoadFromEnvironment()
    {
        return new CacheConfiguration
        {
            IsEnabled = !bool.TryParse(
                Environment.GetEnvironmentVariable("CACHE_ENABLED"), 
                out var enabled) || enabled,
            
            SizeLimitMb = int.TryParse(
                Environment.GetEnvironmentVariable("CACHE_SIZE_MB"), 
                out var size) ? size : 50,
            
            DefaultTtlSeconds = int.TryParse(
                Environment.GetEnvironmentVariable("CACHE_DURATION_SECONDS"), 
                out var duration) ? duration : 300,
            
            NegativeTtlSeconds = int.TryParse(
                Environment.GetEnvironmentVariable("CACHE_NEGATIVE_SECONDS"), 
                out var negativeTtl) ? negativeTtl : 60
        };
    }
}
