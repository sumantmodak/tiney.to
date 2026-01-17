namespace TineyTo.Functions.Configuration;

/// <summary>
/// Configuration for API key authentication
/// </summary>
public class ApiAuthConfiguration
{
    /// <summary>
    /// Enable/disable API key validation for shorten endpoint
    /// </summary>
    public bool IsEnabled { get; init; } = false;

    /// <summary>
    /// Valid API keys for shorten endpoint (comma-separated in env var)
    /// Uses HashSet for O(1) lookup performance
    /// </summary>
    public HashSet<string> ValidApiKeys { get; init; } = [];

    /// <summary>
    /// Admin API keys for admin endpoints (comma-separated)
    /// Future use for admin operations. Uses HashSet for O(1) lookup
    /// </summary>
    public HashSet<string> AdminApiKeys { get; init; } = [];

    /// <summary>
    /// Loads configuration from environment variables
    /// </summary>
    public static ApiAuthConfiguration LoadFromEnvironment()
    {
        var isEnabledStr = Environment.GetEnvironmentVariable("API_AUTH_ENABLED");
        var apiKeysStr = Environment.GetEnvironmentVariable("SHORTEN_API_KEYS") ?? string.Empty;
        var adminKeysStr = Environment.GetEnvironmentVariable("ADMIN_API_KEYS");

        var validKeys = string.IsNullOrWhiteSpace(apiKeysStr)
            ? []
            : apiKeysStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var adminKeys = string.IsNullOrWhiteSpace(adminKeysStr)
            ? []
            : adminKeysStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new ApiAuthConfiguration
        {
            IsEnabled = bool.TryParse(isEnabledStr, out var enabled) && enabled,
            ValidApiKeys = new HashSet<string>(validKeys, StringComparer.Ordinal),
            AdminApiKeys = new HashSet<string>(adminKeys, StringComparer.Ordinal)
        };
    }
}
