namespace TineyTo.Functions.Configuration;

/// <summary>
/// General application configuration settings.
/// </summary>
public class ApplicationConfiguration
{
    /// <summary>
    /// Base URL for generated short URLs (e.g., "https://tiney.to" or "http://localhost:7071").
    /// </summary>
    public string BaseUrl { get; init; } = "http://localhost:7071";

    /// <summary>
    /// Length of generated aliases (default: 6 characters).
    /// </summary>
    public int AliasLength { get; init; } = 6;

    /// <summary>
    /// Maximum TTL for short URLs in seconds (default: 7776000 = 90 days).
    /// </summary>
    public int MaxTtlSeconds { get; init; } = 7776000;

    /// <summary>
    /// Loads application configuration from environment variables.
    /// </summary>
    public static ApplicationConfiguration LoadFromEnvironment()
    {
        return new ApplicationConfiguration
        {
            BaseUrl = (Environment.GetEnvironmentVariable("SHORT_BASE_URL") 
                      ?? Environment.GetEnvironmentVariable("BaseUrl") 
                      ?? "http://localhost:7071").TrimEnd('/'),
            
            AliasLength = int.TryParse(
                Environment.GetEnvironmentVariable("ALIAS_LENGTH"), 
                out var aliasLength) ? aliasLength : 6,
            
            MaxTtlSeconds = int.TryParse(
                Environment.GetEnvironmentVariable("MAX_TTL_SECONDS"), 
                out var maxTtl) ? maxTtl : 2592000
        };
    }
}
