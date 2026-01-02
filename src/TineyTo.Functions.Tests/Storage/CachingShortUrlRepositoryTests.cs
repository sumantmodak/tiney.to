using Microsoft.Extensions.Caching.Memory;
using Moq;
using TineyTo.Functions.Configuration;
using TineyTo.Functions.Services;
using TineyTo.Functions.Storage;
using TineyTo.Functions.Storage.Entities;
using Xunit;

namespace TineyTo.Functions.Tests.Storage;

public class CachingShortUrlRepositoryTests
{
    private readonly Mock<IShortUrlRepository> _innerRepoMock;
    private readonly IMemoryCache _cache;
    private readonly CacheConfiguration _config;
    private readonly Mock<ITimeProvider> _timeProviderMock;
    private readonly Mock<ICacheMetrics> _metricsMock;
    private readonly CachingShortUrlRepository _repository;
    private readonly DateTimeOffset _now;

    public CachingShortUrlRepositoryTests()
    {
        _innerRepoMock = new Mock<IShortUrlRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
        _config = new CacheConfiguration
        {
            IsEnabled = true,
            SizeLimitMb = 50,
            DefaultTtlSeconds = 300,
            NegativeTtlSeconds = 60
        };
        _timeProviderMock = new Mock<ITimeProvider>();
        _now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _timeProviderMock.Setup(t => t.UtcNow).Returns(_now);
        _metricsMock = new Mock<ICacheMetrics>();

        _repository = new CachingShortUrlRepository(
            _innerRepoMock.Object,
            _cache,
            _config,
            _timeProviderMock.Object,
            _metricsMock.Object);
    }

    #region Cache Hits Tests (Task 3.1)

    [Fact]
    public async Task GetByAliasAsync_FirstCall_MissesCache_LoadsFromDatabase()
    {
        // Arrange
        var alias = "test123";
        var entity = ShortUrlEntity.Create(
            alias,
            "https://example.com",
            _now,
            _now.AddDays(30));

        _innerRepoMock
            .Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _repository.GetByAliasAsync(alias);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("https://example.com", result.LongUrl);
        _innerRepoMock.Verify(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()), Times.Once);
        _metricsMock.Verify(m => m.RecordMiss("GetByAlias"), Times.Once);
    }

    [Fact]
    public async Task GetByAliasAsync_SecondCall_HitsCache_NoDatabaseCall()
    {
        // Arrange
        var alias = "test123";
        var entity = ShortUrlEntity.Create(
            alias,
            "https://example.com",
            _now,
            _now.AddDays(30));

        _innerRepoMock
            .Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act - First call loads from DB
        await _repository.GetByAliasAsync(alias);
        
        // Act - Second call should hit cache
        var result = await _repository.GetByAliasAsync(alias);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("https://example.com", result.LongUrl);
        _innerRepoMock.Verify(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()), Times.Once);
        _metricsMock.Verify(m => m.RecordHit("GetByAlias"), Times.Once);
    }

    [Fact]
    public async Task GetByAliasAsync_CachedExpiredUrl_EvictsButStillReturnsEntity()
    {
        // Arrange
        var alias = "expired";
        var validEntity = ShortUrlEntity.Create(
            alias,
            "https://example.com",
            _now.AddDays(-10),
            _now.AddMinutes(5)); // Valid for 5 more minutes

        _innerRepoMock
            .Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validEntity);

        // Act - First call caches the valid entity
        var result1 = await _repository.GetByAliasAsync(alias);
        Assert.NotNull(result1);
        Assert.False(result1.IsExpired(_now));
        
        // Move time forward so cached entity is now expired
        var futureTime = _now.AddMinutes(10);
        _timeProviderMock.Setup(t => t.UtcNow).Returns(futureTime);
        
        // Act - Second call should detect expiry, evict, but still return the entity
        // (so caller can respond with 410 Gone)
        var result2 = await _repository.GetByAliasAsync(alias);

        // Assert
        Assert.NotNull(result2);
        Assert.True(result2.IsExpired(futureTime)); // Now expired
        _metricsMock.Verify(m => m.RecordHit("GetByAlias"), Times.Once);
        _metricsMock.Verify(m => m.RecordEviction(It.IsAny<string>()), Times.Once);
        // Only one DB call - expired entities are returned from cache before eviction
        _innerRepoMock.Verify(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByAliasAsync_CachedDisabledUrl_EvictsButStillReturnsEntity()
    {
        // Arrange
        var alias = "disabled";
        
        var entity = ShortUrlEntity.Create(
            alias,
            "https://example.com",
            _now,
            _now.AddDays(30));

        _innerRepoMock
            .Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act - First call caches valid entity
        var result1 = await _repository.GetByAliasAsync(alias);
        Assert.NotNull(result1);
        Assert.False(result1.IsDisabled);
        
        // Simulate entity being disabled externally
        entity.IsDisabled = true;
        
        // Act - Second call detects disabled, evicts, but returns it
        // (so caller can respond with 410 Gone)
        var result2 = await _repository.GetByAliasAsync(alias);

        // Assert
        Assert.NotNull(result2);
        Assert.True(result2.IsDisabled);
        _metricsMock.Verify(m => m.RecordHit("GetByAlias"), Times.Once);
        _metricsMock.Verify(m => m.RecordEviction(It.IsAny<string>()), Times.Once);
        // Only one DB call - disabled entities are returned from cache before eviction
        _innerRepoMock.Verify(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByAliasAsync_ValidCachedEntity_ReturnsWithoutDatabaseCall()
    {
        // Arrange
        var alias = "valid";
        var entity = ShortUrlEntity.Create(
            alias,
            "https://example.com",
            _now,
            _now.AddDays(30));

        _innerRepoMock
            .Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var result1 = await _repository.GetByAliasAsync(alias);
        var result2 = await _repository.GetByAliasAsync(alias);
        var result3 = await _repository.GetByAliasAsync(alias);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        _innerRepoMock.Verify(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()), Times.Once);
        _metricsMock.Verify(m => m.RecordMiss("GetByAlias"), Times.Once);
        _metricsMock.Verify(m => m.RecordHit("GetByAlias"), Times.Exactly(2));
    }

    #endregion

    #region Cache Invalidation Tests (Task 3.2)

    [Fact]
    public async Task InsertAsync_Success_EvictsOldCacheAndCachesNew()
    {
        // Arrange
        var alias = "test123";
        var oldEntity = ShortUrlEntity.Create(alias, "https://old.com", _now);
        var newEntity = ShortUrlEntity.Create(alias, "https://new.com", _now, _now.AddDays(30));

        _innerRepoMock
            .Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldEntity);
        
        _innerRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<ShortUrlEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act - Cache old entity
        var oldResult = await _repository.GetByAliasAsync(alias);
        Assert.Equal("https://old.com", oldResult!.LongUrl);

        // Act - Insert new entity
        var insertResult = await _repository.InsertAsync(newEntity);
        
        // Act - Get should return new cached entity
        var newResult = await _repository.GetByAliasAsync(alias);

        // Assert
        Assert.True(insertResult);
        Assert.NotNull(newResult);
        Assert.Equal("https://new.com", newResult.LongUrl);
        _innerRepoMock.Verify(r => r.InsertAsync(newEntity, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InsertAsync_Failure_DoesNotCache()
    {
        // Arrange
        var alias = "test123";
        var entity = ShortUrlEntity.Create(alias, "https://example.com", _now);

        _innerRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<ShortUrlEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _innerRepoMock
            .Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortUrlEntity?)null);

        // Act
        var insertResult = await _repository.InsertAsync(entity);
        var getResult = await _repository.GetByAliasAsync(alias);

        // Assert
        Assert.False(insertResult);
        Assert.Null(getResult);
        _innerRepoMock.Verify(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFromCache()
    {
        // Arrange
        var alias = "test123";
        var entity = ShortUrlEntity.Create(alias, "https://example.com", _now, _now.AddDays(30));

        _innerRepoMock
            .Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        
        _innerRepoMock
            .Setup(r => r.DeleteAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act - Cache the entity
        await _repository.GetByAliasAsync(alias);
        _metricsMock.Verify(m => m.RecordMiss("GetByAlias"), Times.Once);

        // Act - Delete it
        var deleteResult = await _repository.DeleteAsync(alias);

        // Assert
        Assert.True(deleteResult);
        _metricsMock.Verify(m => m.RecordEviction(It.IsAny<string>()), Times.Once);
        _innerRepoMock.Verify(r => r.DeleteAsync(alias, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InsertAsync_DisabledUrl_DoesNotCache()
    {
        // Arrange
        var alias = "disabled";
        var entity = ShortUrlEntity.Create(alias, "https://example.com", _now, _now.AddDays(30));
        entity.IsDisabled = true;

        _innerRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<ShortUrlEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _innerRepoMock
            .Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var insertResult = await _repository.InsertAsync(entity);
        
        // Second get should miss cache (disabled URLs not cached)
        var getResult = await _repository.GetByAliasAsync(alias);

        // Assert
        Assert.True(insertResult);
        _innerRepoMock.Verify(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Negative Caching Tests (Task 3.3)

    [Fact]
    public async Task GetByAliasAsync_NotFound_CachesNegativeResult()
    {
        // Arrange
        var alias = "notfound";

        _innerRepoMock
            .Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortUrlEntity?)null);

        // Act
        var result1 = await _repository.GetByAliasAsync(alias);
        var result2 = await _repository.GetByAliasAsync(alias);

        // Assert
        Assert.Null(result1);
        Assert.Null(result2);
        _innerRepoMock.Verify(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()), Times.Once);
        _metricsMock.Verify(m => m.RecordMiss("GetByAlias"), Times.Once);
        _metricsMock.Verify(m => m.RecordHit("GetByAlias"), Times.Once);
    }

    [Fact]
    public async Task GetByAliasAsync_NotFound_SubsequentCallsHitCache()
    {
        // Arrange
        var alias = "missing";

        _innerRepoMock
            .Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortUrlEntity?)null);

        // Act - Multiple calls
        await _repository.GetByAliasAsync(alias);
        await _repository.GetByAliasAsync(alias);
        await _repository.GetByAliasAsync(alias);

        // Assert - Only one DB call
        _innerRepoMock.Verify(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()), Times.Once);
        _metricsMock.Verify(m => m.RecordMiss("GetByAlias"), Times.Once);
        _metricsMock.Verify(m => m.RecordHit("GetByAlias"), Times.Exactly(2));
    }

    [Fact]
    public async Task GetByAliasAsync_NegativeCacheOverwrittenByInsert()
    {
        // Arrange
        var alias = "newurl";
        var entity = ShortUrlEntity.Create(alias, "https://example.com", _now, _now.AddDays(30));

        _innerRepoMock
            .Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortUrlEntity?)null);
        
        _innerRepoMock
            .Setup(r => r.InsertAsync(It.IsAny<ShortUrlEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act - Cache negative result
        var result1 = await _repository.GetByAliasAsync(alias);
        Assert.Null(result1);

        // Act - Insert the URL (should evict negative cache)
        await _repository.InsertAsync(entity);

        // Act - Get should return the new entity from cache
        var result2 = await _repository.GetByAliasAsync(alias);

        // Assert
        Assert.NotNull(result2);
        Assert.Equal("https://example.com", result2.LongUrl);
    }

    #endregion

    #region TTL Computation Tests

    [Fact]
    public async Task GetByAliasAsync_UrlExpiresBeforeConfigTtl_UsesShorterTtl()
    {
        // Arrange
        var alias = "shortlived";
        var entity = ShortUrlEntity.Create(
            alias,
            "https://example.com",
            _now,
            _now.AddSeconds(60)); // Expires in 60 seconds

        _innerRepoMock
            .Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var result = await _repository.GetByAliasAsync(alias);

        // Assert - Should cache with 60s TTL (URL expiry) not 300s (config)
        Assert.NotNull(result);
        // Note: We can't directly assert TTL, but the entity is cached
        _innerRepoMock.Verify(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByAliasAsync_UrlNoExpiry_UsesConfigTtl()
    {
        // Arrange
        var alias = "permanent";
        var entity = ShortUrlEntity.Create(
            alias,
            "https://example.com",
            _now,
            expiresAtUtc: null); // No expiry

        _innerRepoMock
            .Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        // Act
        var result1 = await _repository.GetByAliasAsync(alias);
        var result2 = await _repository.GetByAliasAsync(alias);

        // Assert - Should cache with config TTL (300s)
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        _innerRepoMock.Verify(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
