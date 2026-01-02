namespace TineyTo.Functions.Services;

/// <summary>
/// Interface for tracking cache performance metrics.
/// </summary>
public interface ICacheMetrics
{
    /// <summary>
    /// Records a cache hit for the specified operation.
    /// </summary>
    /// <param name="operation">The operation that resulted in a cache hit (e.g., "GetByAlias")</param>
    /// <param name="alias">The alias being accessed</param>
    /// <param name="longUrl">The long URL associated with the alias (optional)</param>
    void RecordHit(string operation, string? alias = null, string? longUrl = null);

    /// <summary>
    /// Records a cache miss for the specified operation.
    /// </summary>
    /// <param name="operation">The operation that resulted in a cache miss (e.g., "GetByAlias")</param>
    /// <param name="alias">The alias being accessed</param>
    void RecordMiss(string operation, string? alias = null);

    /// <summary>
    /// Records a cache eviction for the specified key.
    /// </summary>
    /// <param name="key">The cache key that was evicted</param>
    /// <param name="alias">The alias being evicted</param>
    void RecordEviction(string key, string? alias = null);
}
