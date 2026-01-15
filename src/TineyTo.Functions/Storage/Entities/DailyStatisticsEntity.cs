using Azure;
using Azure.Data.Tables;

namespace TineyTo.Functions.Storage.Entities;

/// <summary>
/// Table Storage entity for daily statistics.
/// PartitionKey: "DailyStats", RowKey: {yyyy-MM-dd}
/// </summary>
public class DailyStatisticsEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "DailyStats";
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public long LinksCreated { get; set; }
    public long Redirections { get; set; }

    public static DailyStatisticsEntity Create(DateOnly date)
    {
        return new DailyStatisticsEntity
        {
            RowKey = date.ToString("yyyy-MM-dd"),
            LinksCreated = 0,
            Redirections = 0
        };
    }
}
