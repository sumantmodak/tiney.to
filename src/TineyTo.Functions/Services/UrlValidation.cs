using TineyTo.Functions.Configuration;

namespace TineyTo.Functions.Services;

public interface IUrlValidator
{
    /// <summary>
    /// Validates a long URL.
    /// </summary>
    (bool IsValid, string? Error) ValidateLongUrl(string? url);

    /// <summary>
    /// Validates the expires in seconds value.
    /// </summary>
    (bool IsValid, string? Error) ValidateExpiresInSeconds(int? expiresInSeconds);
}

public class UrlValidator : IUrlValidator
{
    private const int MaxUrlLength = 4096;
    private const int MinTtlSeconds = 60;
    private readonly int _maxTtlSeconds;

    public UrlValidator(ApplicationConfiguration config)
    {
        _maxTtlSeconds = config.MaxTtlSeconds;
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
        if (uri.Scheme == "http")
        {
            return (false, "longUrl must use https scheme");
        }
        if (uri.Scheme != "https")
        {
            return (false, "longUrl must use https scheme");
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
