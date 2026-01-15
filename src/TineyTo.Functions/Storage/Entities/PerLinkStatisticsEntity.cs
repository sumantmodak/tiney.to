using Azure;
using Azure.Data.Tables;

namespace TineyTo.Functions.Storage.Entities;

/// <summary>
/// Table Storage entity for per-link statistics.
/// PartitionKey: {alias}, RowKey: "stats"
/// </summary>
public class PerLinkStatisticsEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = "stats";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Alias { get; set; } = string.Empty;
    public long RedirectionCount { get; set; }
    public DateTimeOffset? FirstRedirect { get; set; }
    public DateTimeOffset? LastRedirect { get; set; }

    public static PerLinkStatisticsEntity Create(string alias, DateTimeOffset timestamp)
    {
        return new PerLinkStatisticsEntity
        {
            PartitionKey = alias,
            Alias = alias,
            RedirectionCount = 0,
            FirstRedirect = timestamp,
            LastRedirect = timestamp
        };
    }
}
