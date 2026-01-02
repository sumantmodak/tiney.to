namespace TineyTo.Functions.Configuration;

/// <summary>
/// Azure Storage configuration settings.
/// </summary>
public class StorageConfiguration
{
    /// <summary>
    /// Connection string for Azure Table Storage.
    /// </summary>
    public string TableConnection { get; init; } = "UseDevelopmentStorage=true";

    /// <summary>
    /// Name of the main ShortUrls table.
    /// </summary>
    public string ShortUrlTableName { get; init; } = "ShortUrls";

    /// <summary>
    /// Name of the expiry index table.
    /// </summary>
    public string ExpiryIndexTableName { get; init; } = "ShortUrlsExpiryIndex";

    /// <summary>
    /// Name of the URL index table (for deduplication).
    /// </summary>
    public string UrlIndexTableName { get; init; } = "UrlIndex";

    /// <summary>
    /// Connection string for the blob storage used for distributed locks.
    /// </summary>
    public string GcBlobLockConnection { get; init; } = "UseDevelopmentStorage=true";

    /// <summary>
    /// Container name for the GC lock blob.
    /// </summary>
    public string GcBlobLockContainer { get; init; } = "locks";

    /// <summary>
    /// Blob name for the GC lock.
    /// </summary>
    public string GcBlobLockBlob { get; init; } = "expiry-reaper.lock";

    /// <summary>
    /// Loads storage configuration from environment variables.
    /// </summary>
    public static StorageConfiguration LoadFromEnvironment()
    {
        return new StorageConfiguration
        {
            TableConnection = Environment.GetEnvironmentVariable("TABLE_CONNECTION") 
                             ?? "UseDevelopmentStorage=true",
            
            ShortUrlTableName = Environment.GetEnvironmentVariable("SHORTURL_TABLE_NAME") 
                               ?? "ShortUrls",
            
            ExpiryIndexTableName = Environment.GetEnvironmentVariable("EXPIRYINDEX_TABLE_NAME") 
                                  ?? "ShortUrlsExpiryIndex",
            
            UrlIndexTableName = Environment.GetEnvironmentVariable("URLINDEX_TABLE_NAME") 
                               ?? "UrlIndex",
            
            GcBlobLockConnection = Environment.GetEnvironmentVariable("GC_BLOB_LOCK_CONNECTION") 
                                  ?? "UseDevelopmentStorage=true",
            
            GcBlobLockContainer = Environment.GetEnvironmentVariable("GC_BLOB_LOCK_CONTAINER") 
                                 ?? "locks",
            
            GcBlobLockBlob = Environment.GetEnvironmentVariable("GC_BLOB_LOCK_BLOB") 
                            ?? "expiry-reaper.lock"
        };
    }
}
