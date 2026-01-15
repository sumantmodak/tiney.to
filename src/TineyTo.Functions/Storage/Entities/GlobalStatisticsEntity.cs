using Azure;
using Azure.Data.Tables;

namespace TineyTo.Functions.Storage.Entities;

/// <summary>
/// Table Storage entity for global statistics totals.
/// PartitionKey: "GlobalStats", RowKey: "totals"
/// </summary>
public class GlobalStatisticsEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "GlobalStats";
    public string RowKey { get; set; } = "totals";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public long TotalLinksShortened { get; set; }
    public long TotalRedirections { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public static GlobalStatisticsEntity Create()
    {
        return new GlobalStatisticsEntity
        {
            TotalLinksShortened = 0,
            TotalRedirections = 0,
            LastUpdated = DateTimeOffset.UtcNow
        };
    }
}
