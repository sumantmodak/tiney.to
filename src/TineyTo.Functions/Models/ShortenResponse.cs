using System.Text.Json.Serialization;

namespace TineyTo.Functions.Models;

public class ShortenResponse
{
    [JsonPropertyName("alias")]
    public string Alias { get; set; } = string.Empty;

    [JsonPropertyName("shortUrl")]
    public string ShortUrl { get; set; } = string.Empty;

    [JsonPropertyName("longUrl")]
    public string LongUrl { get; set; } = string.Empty;

    [JsonPropertyName("createdAtUtc")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [JsonPropertyName("expiresAtUtc")]
    public DateTimeOffset? ExpiresAtUtc { get; set; }
}
