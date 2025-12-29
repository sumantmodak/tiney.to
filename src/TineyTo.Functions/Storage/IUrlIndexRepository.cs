using TineyTo.Functions.Storage.Entities;

namespace TineyTo.Functions.Storage;

/// <summary>
/// Repository for managing URL index entries for deduplication.
/// </summary>
public interface IUrlIndexRepository
{
    /// <summary>
    /// Gets a URL index entry by long URL.
    /// Returns null if not found.
    /// </summary>
    Task<UrlIndexEntity?> GetByLongUrlAsync(string longUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a new URL index entry.
    /// Returns true if successful, false if entry already exists (conflict).
    /// </summary>
    Task<bool> InsertAsync(UrlIndexEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a URL index entry by long URL.
    /// Returns true if deleted, false if not found.
    /// </summary>
    Task<bool> DeleteAsync(string longUrl, CancellationToken cancellationToken = default);
}
