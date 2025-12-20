using TineyTo.Functions.Storage.Entities;

namespace TineyTo.Functions.Storage;

public interface IExpiryIndexRepository
{
    /// <summary>
    /// Inserts a new expiry index entity.
    /// </summary>
    Task InsertAsync(ExpiryIndexEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all expired entities for the given date partition that have expired before the given time.
    /// </summary>
    Task<IReadOnlyList<ExpiryIndexEntity>> GetExpiredAsync(
        string datePartition, 
        DateTimeOffset beforeUtc, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an expiry index entity.
    /// Returns true if deleted, false if not found.
    /// </summary>
    Task<bool> DeleteAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default);
}
