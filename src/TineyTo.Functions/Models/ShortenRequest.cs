using System.Text.Json.Serialization;

namespace TineyTo.Functions.Models;

public class ShortenRequest
{
    [JsonPropertyName("longUrl")]
    public string LongUrl { get; set; } = string.Empty;

    [JsonPropertyName("expiresInSeconds")]
    public int? ExpiresInSeconds { get; set; }
}
