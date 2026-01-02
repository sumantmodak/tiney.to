using Azure;
using Azure.Data.Tables;
using System.Security.Cryptography;
using System.Text;

namespace TineyTo.Functions.Storage.Entities;

/// <summary>
/// Entity for the UrlIndex table, used to deduplicate long URLs.
/// Allows quick lookup to see if a long URL already has a short alias.
/// </summary>
public class UrlIndexEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string LongUrl { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAtUtc { get; set; }

    /// <summary>
    /// Computes a partition key from a long URL.
    /// Uses a MD5 stable hash to distribute URLs across partitions. D2 takes the first two digits (00-99).
    /// </summary>
    public static string ComputePartitionKey(string longUrl)
    {
        if (string.IsNullOrEmpty(longUrl))
            return "00";
        
        // Use hash code to distribute URLs across partitions (00-99)
        var hash = Math.Abs(BitConverter.ToInt32(MD5.HashData(Encoding.UTF8.GetBytes(longUrl)), 0));
        var partition = (hash % 100).ToString("D2");
        return partition;
    }

    /// <summary>
    /// Computes a SHA256 hash of the URL to use as RowKey.
    /// </summary>
    public static string ComputeRowKey(string longUrl)
    {
        if (string.IsNullOrEmpty(longUrl))
            return string.Empty;
        
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(longUrl));
        return Convert.ToHexString(hash);
    }

    public static (string, string) ComputePartitionAndRowKey(string longUrl)
    {
        return (ComputePartitionKey(longUrl), ComputeRowKey(longUrl));
    }

    /// <summary>
    /// Creates a new UrlIndexEntity from the provided parameters.
    /// The RowKey is a SHA256 hash of the URL to stay within size limits.
    /// </summary>
    public static UrlIndexEntity Create(
        string longUrl,
        string alias,
        DateTimeOffset? expiresAtUtc = null)
    {
        var (pkey, rkey) = ComputePartitionAndRowKey(longUrl);
        return new UrlIndexEntity
        {
            PartitionKey = pkey,
            RowKey = rkey,
            LongUrl = longUrl,
            Alias = alias,
            ExpiresAtUtc = expiresAtUtc
        };
    }
}
