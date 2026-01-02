using Microsoft.Extensions.Caching.Memory;
using TineyTo.Functions.Configuration;
using TineyTo.Functions.Services;
using TineyTo.Functions.Storage.Entities;

namespace TineyTo.Functions.Storage;

/// <summary>
/// Caching decorator for IShortUrlRepository that uses IMemoryCache.
/// Implements cache-aside pattern for reads and write-through for writes.
/// </summary>
public class CachingShortUrlRepository : IShortUrlRepository
{
    private readonly IShortUrlRepository _inner;
    private readonly IMemoryCache _cache;
    private readonly CacheConfiguration _config;
    private readonly ITimeProvider _timeProvider;
    private readonly ICacheMetrics _metrics;

    private const string CacheKeyPrefix = "shorturl:alias:";
    private const string NegativeCacheValue = "__NOT_FOUND__";

    public CachingShortUrlRepository(
        IShortUrlRepository inner,
        IMemoryCache cache,
        CacheConfiguration config,
        ITimeProvider timeProvider,
        ICacheMetrics metrics)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    /// <summary>
    /// Gets a short URL by alias using cache-aside pattern.
    /// Checks cache first, validates expiry, and falls back to database on miss.
    /// </summary>
    public async Task<ShortUrlEntity?> GetByAliasAsync(string alias, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(alias);

        // Try to get from cache
        if (_cache.TryGetValue<object>(cacheKey, out var cachedValue))
        {
            // Check for negative cache (404)
            if (cachedValue is string s && s == NegativeCacheValue)
            {
                _metrics.RecordHit("GetByAlias", alias);
                return null;
            }

            var entity = cachedValue as ShortUrlEntity;
            if (entity != null)
            {
                var now = _timeProvider.UtcNow;

                // Validate entity is still valid (not expired or disabled)
                if (entity.IsDisabled || entity.IsExpired(now))
                {
                    // Entity is stale, evict from cache
                    _cache.Remove(cacheKey);
                    _metrics.RecordEviction(cacheKey, alias);
                    
                    // Return appropriate result
                    return entity;
                }

                _metrics.RecordHit("GetByAlias", alias, entity.LongUrl);
                return entity;
            }
        }

        // Cache miss - load from database
        _metrics.RecordMiss("GetByAlias", alias);
        var result = await _inner.GetByAliasAsync(alias, cancellationToken);

        if (result == null)
        {
            // Cache negative result (404) with short TTL
            var negativeTtl = TimeSpan.FromSeconds(_config.NegativeTtlSeconds);
            _cache.Set(cacheKey, NegativeCacheValue, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = negativeTtl,
                Size = 1
            });
        }
        else
        {
            // Cache successful result with smart TTL
            var ttl = ComputeCacheTtl(result);
            if (ttl > TimeSpan.Zero)
            {
                _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl,
                    Size = 1
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Inserts a new short URL entity with write-through caching.
    /// Invalidates any existing cache entry and caches the new entity on success.
    /// </summary>
    public async Task<bool> InsertAsync(ShortUrlEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var cacheKey = GetCacheKey(entity.RowKey);

        // Write to database first
        var success = await _inner.InsertAsync(entity, cancellationToken);

        if (success)
        {
            // Evict old cache entry (could be negative cache or stale data)
            _cache.Remove(cacheKey);

            // Cache the new entity with smart TTL
            var ttl = ComputeCacheTtl(entity);
            if (ttl > TimeSpan.Zero)
            {
                _cache.Set(cacheKey, entity, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl,
                    Size = 1
                });
            }
        }

        return success;
    }

    /// <summary>
    /// Deletes a short URL entity with write-through caching.
    /// Evicts from cache before deleting from database.
    /// </summary>
    public async Task<bool> DeleteAsync(string alias, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(alias);

        // Evict from cache proactively
        _cache.Remove(cacheKey);
        _metrics.RecordEviction(cacheKey, alias);

        // Delete from database
        return await _inner.DeleteAsync(alias, cancellationToken);
    }

    /// <summary>
    /// Computes a smart cache TTL based on URL expiry and configuration.
    /// Returns the minimum of: config TTL or time until URL expires.
    /// </summary>
    private TimeSpan ComputeCacheTtl(ShortUrlEntity entity)
    {
        // If disabled, don't cache at all
        if (entity.IsDisabled)
        {
            return TimeSpan.Zero;
        }

        // Start with configured TTL
        var ttl = TimeSpan.FromSeconds(_config.DefaultTtlSeconds);

        // If URL has expiry, don't cache beyond it
        if (entity.ExpiresAtUtc.HasValue)
        {
            var now = _timeProvider.UtcNow;
            var timeUntilExpiry = entity.ExpiresAtUtc.Value - now;

            if (timeUntilExpiry < ttl)
            {
                ttl = timeUntilExpiry;
            }
        }

        return ttl > TimeSpan.Zero ? ttl : TimeSpan.Zero;
    }

    /// <summary>
    /// Generates a cache key for the given alias.
    /// </summary>
    private static string GetCacheKey(string alias)
    {
        return $"{CacheKeyPrefix}{alias}";
    }
}
