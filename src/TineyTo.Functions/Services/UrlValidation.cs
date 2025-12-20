using System.Text.RegularExpressions;

namespace TineyTo.Functions.Services;

public interface IUrlValidator
{
    /// <summary>
    /// Validates a long URL.
    /// </summary>
    (bool IsValid, string? Error) ValidateLongUrl(string? url);

    /// <summary>
    /// Validates a custom alias.
    /// </summary>
    (bool IsValid, string? Error) ValidateCustomAlias(string? alias);

    /// <summary>
    /// Validates the expires in seconds value.
    /// </summary>
    (bool IsValid, string? Error) ValidateExpiresInSeconds(int? expiresInSeconds);
}

public partial class UrlValidator : IUrlValidator
{
    private const int MaxUrlLength = 4096;
    private const int MinTtlSeconds = 60;
    private readonly int _maxTtlSeconds;

    [GeneratedRegex(@"^[A-Za-z0-9_-]{3,32}$")]
    private static partial Regex AliasRegex();

    public UrlValidator()
    {
        var maxTtlStr = Environment.GetEnvironmentVariable("MAX_TTL_SECONDS") ?? "7776000";
        _maxTtlSeconds = int.TryParse(maxTtlStr, out var maxTtl) ? maxTtl : 7776000; // 90 days
    }

    public (bool IsValid, string? Error) ValidateLongUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return (false, "longUrl is required");
        }

        if (url.Length > MaxUrlLength)
        {
            return (false, $"longUrl must not exceed {MaxUrlLength} characters");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return (false, "longUrl must be a valid absolute URL");
        }

        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            return (false, "longUrl must use http or https scheme");
        }

        return (true, null);
    }

    public (bool IsValid, string? Error) ValidateCustomAlias(string? alias)
    {
        if (string.IsNullOrEmpty(alias))
        {
            return (true, null); // Custom alias is optional
        }

        if (!AliasRegex().IsMatch(alias))
        {
            return (false, "customAlias must be 3-32 characters and contain only letters, numbers, hyphens, and underscores");
        }

        return (true, null);
    }

    public (bool IsValid, string? Error) ValidateExpiresInSeconds(int? expiresInSeconds)
    {
        if (!expiresInSeconds.HasValue)
        {
            return (true, null); // Expiry is optional
        }

        if (expiresInSeconds.Value < MinTtlSeconds)
        {
            return (false, $"expiresInSeconds must be at least {MinTtlSeconds} seconds");
        }

        if (expiresInSeconds.Value > _maxTtlSeconds)
        {
            return (false, $"expiresInSeconds must not exceed {_maxTtlSeconds} seconds");
        }

        return (true, null);
    }
}
