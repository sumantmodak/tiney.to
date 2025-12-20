using Azure;
using Azure.Data.Tables;

namespace TineyTo.Functions.Storage.Entities;

public class ShortUrlEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string LongUrl { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public bool IsDisabled { get; set; }
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Computes the partition key from an alias.
    /// Uses first 2 characters (lowercased), or "xx" if alias is too short.
    /// </summary>
    public static string ComputePartitionKey(string alias)
    {
        if (string.IsNullOrEmpty(alias) || alias.Length < 2)
            return "xx";
        
        return alias.Substring(0, 2).ToLowerInvariant();
    }

    /// <summary>
    /// Creates a new ShortUrlEntity from the provided parameters.
    /// </summary>
    public static ShortUrlEntity Create(
        string alias,
        string longUrl,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? expiresAtUtc = null,
        string? createdBy = null)
    {
        return new ShortUrlEntity
        {
            PartitionKey = ComputePartitionKey(alias),
            RowKey = alias,
            LongUrl = longUrl,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc,
            IsDisabled = false,
            CreatedBy = createdBy
        };
    }

    /// <summary>
    /// Checks if this short URL has expired.
    /// </summary>
    public bool IsExpired(DateTimeOffset now)
    {
        return ExpiresAtUtc.HasValue && ExpiresAtUtc.Value < now;
    }
}
