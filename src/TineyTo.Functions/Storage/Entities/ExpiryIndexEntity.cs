using Azure;
using Azure.Data.Tables;

namespace TineyTo.Functions.Storage.Entities;

public class ExpiryIndexEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string AliasPartitionKey { get; set; } = string.Empty;
    public string AliasRowKey { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }

    /// <summary>
    /// Computes the partition key from an expiry date (yyyyMMdd format).
    /// </summary>
    public static string ComputePartitionKey(DateTimeOffset expiresAtUtc)
    {
        return expiresAtUtc.UtcDateTime.ToString("yyyyMMdd");
    }

    /// <summary>
    /// Computes the row key from expiry time and alias (HHmmss|alias format).
    /// </summary>
    public static string ComputeRowKey(DateTimeOffset expiresAtUtc, string alias)
    {
        var timeComponent = expiresAtUtc.UtcDateTime.ToString("HHmmss");
        return $"{timeComponent}|{alias}";
    }

    /// <summary>
    /// Creates a new ExpiryIndexEntity from the provided parameters.
    /// </summary>
    public static ExpiryIndexEntity Create(string alias, DateTimeOffset expiresAtUtc)
    {
        return new ExpiryIndexEntity
        {
            PartitionKey = ComputePartitionKey(expiresAtUtc),
            RowKey = ComputeRowKey(expiresAtUtc, alias),
            AliasPartitionKey = ShortUrlEntity.ComputePartitionKey(alias),
            AliasRowKey = alias,
            ExpiresAtUtc = expiresAtUtc
        };
    }
}
