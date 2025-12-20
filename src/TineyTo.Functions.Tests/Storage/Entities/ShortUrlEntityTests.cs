using TineyTo.Functions.Storage.Entities;

namespace TineyTo.Functions.Tests.Storage.Entities;

public class ShortUrlEntityTests
{
    #region ComputePartitionKey Tests

    [Theory]
    [InlineData("abcdef", "ab")]
    [InlineData("ABCDEF", "ab")]
    [InlineData("Ab1234", "ab")]
    [InlineData("12abcd", "12")]
    public void ComputePartitionKey_ValidAlias_ReturnsFirst2CharsLowercased(string alias, string expected)
    {
        var result = ShortUrlEntity.ComputePartitionKey(alias);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    public void ComputePartitionKey_ShortOrNullAlias_ReturnsDefaultPartition(string? alias)
    {
        var result = ShortUrlEntity.ComputePartitionKey(alias!);

        Assert.Equal("xx", result);
    }

    #endregion

    #region Create Tests

    [Fact]
    public void Create_WithRequiredParams_SetsPropertiesCorrectly()
    {
        var alias = "testAlias";
        var longUrl = "https://example.com";
        var createdAt = DateTimeOffset.UtcNow;

        var entity = ShortUrlEntity.Create(alias, longUrl, createdAt);

        Assert.Equal("te", entity.PartitionKey);
        Assert.Equal(alias, entity.RowKey);
        Assert.Equal(longUrl, entity.LongUrl);
        Assert.Equal(createdAt, entity.CreatedAtUtc);
        Assert.Null(entity.ExpiresAtUtc);
        Assert.False(entity.IsDisabled);
        Assert.Null(entity.CreatedBy);
    }

    [Fact]
    public void Create_WithAllParams_SetsPropertiesCorrectly()
    {
        var alias = "testAlias";
        var longUrl = "https://example.com";
        var createdAt = DateTimeOffset.UtcNow;
        var expiresAt = createdAt.AddDays(7);
        var createdBy = "user123";

        var entity = ShortUrlEntity.Create(alias, longUrl, createdAt, expiresAt, createdBy);

        Assert.Equal("te", entity.PartitionKey);
        Assert.Equal(alias, entity.RowKey);
        Assert.Equal(longUrl, entity.LongUrl);
        Assert.Equal(createdAt, entity.CreatedAtUtc);
        Assert.Equal(expiresAt, entity.ExpiresAtUtc);
        Assert.False(entity.IsDisabled);
        Assert.Equal(createdBy, entity.CreatedBy);
    }

    #endregion

    #region IsExpired Tests

    [Fact]
    public void IsExpired_NoExpirySet_ReturnsFalse()
    {
        var entity = ShortUrlEntity.Create("test", "https://example.com", DateTimeOffset.UtcNow);

        var result = entity.IsExpired(DateTimeOffset.UtcNow);

        Assert.False(result);
    }

    [Fact]
    public void IsExpired_ExpiryInFuture_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = ShortUrlEntity.Create("test", "https://example.com", now, now.AddHours(1));

        var result = entity.IsExpired(now);

        Assert.False(result);
    }

    [Fact]
    public void IsExpired_ExpiryInPast_ReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = ShortUrlEntity.Create("test", "https://example.com", now.AddHours(-2), now.AddHours(-1));

        var result = entity.IsExpired(now);

        Assert.True(result);
    }

    [Fact]
    public void IsExpired_ExpiryExactlyNow_ReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new ShortUrlEntity { ExpiresAtUtc = now };

        // now < now is false, so exact match should return false
        var result = entity.IsExpired(now);

        Assert.False(result);
    }

    #endregion
}
