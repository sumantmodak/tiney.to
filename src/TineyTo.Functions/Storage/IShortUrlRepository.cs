using TineyTo.Functions.Storage.Entities;

namespace TineyTo.Functions.Storage;

public interface IShortUrlRepository
{
    /// <summary>
    /// Gets a short URL entity by alias.
    /// </summary>
    Task<ShortUrlEntity?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new short URL entity.
    /// Returns true if successful, false if entity already exists (conflict).
    /// </summary>
    Task<bool> InsertAsync(ShortUrlEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a short URL entity by alias.
    /// Returns true if deleted, false if not found.
    /// </summary>
    Task<bool> DeleteAsync(string alias, CancellationToken cancellationToken = default);
}
