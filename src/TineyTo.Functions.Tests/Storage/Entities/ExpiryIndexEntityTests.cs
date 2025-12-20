using TineyTo.Functions.Storage.Entities;

namespace TineyTo.Functions.Tests.Storage.Entities;

public class ExpiryIndexEntityTests
{
    #region ComputePartitionKey Tests

    [Fact]
    public void ComputePartitionKey_ReturnsDateInYyyyMMddFormat()
    {
        var date = new DateTimeOffset(2025, 12, 20, 14, 30, 45, TimeSpan.Zero);

        var result = ExpiryIndexEntity.ComputePartitionKey(date);

        Assert.Equal("20251220", result);
    }

    [Fact]
    public void ComputePartitionKey_UsesUtcDate()
    {
        // 11 PM on Dec 20 in UTC+5 = Dec 20 6 PM UTC
        var dateWithOffset = new DateTimeOffset(2025, 12, 20, 23, 0, 0, TimeSpan.FromHours(5));

        var result = ExpiryIndexEntity.ComputePartitionKey(dateWithOffset);

        // Should use UTC date (Dec 20), not local date
        Assert.Equal("20251220", result);
    }

    #endregion

    #region ComputeRowKey Tests

    [Fact]
    public void ComputeRowKey_ReturnsTimeAndAlias()
    {
        var date = new DateTimeOffset(2025, 12, 20, 8, 30, 0, TimeSpan.Zero);
        var alias = "myAlias";

        var result = ExpiryIndexEntity.ComputeRowKey(date, alias);

        Assert.Equal("083000|myAlias", result);
    }

    [Fact]
    public void ComputeRowKey_PadsTimeWithZeros()
    {
        var date = new DateTimeOffset(2025, 12, 20, 1, 5, 9, TimeSpan.Zero);
        var alias = "test";

        var result = ExpiryIndexEntity.ComputeRowKey(date, alias);

        Assert.Equal("010509|test", result);
    }

    #endregion

    #region Create Tests

    [Fact]
    public void Create_SetsAllPropertiesCorrectly()
    {
        var alias = "testAlias";
        var expiresAt = new DateTimeOffset(2025, 12, 20, 14, 30, 45, TimeSpan.Zero);

        var entity = ExpiryIndexEntity.Create(alias, expiresAt);

        Assert.Equal("20251220", entity.PartitionKey);
        Assert.Equal("143045|testAlias", entity.RowKey);
        Assert.Equal("te", entity.AliasPartitionKey);
        Assert.Equal(alias, entity.AliasRowKey);
        Assert.Equal(expiresAt, entity.ExpiresAtUtc);
    }

    [Fact]
    public void Create_ShortAlias_UsesDefaultPartition()
    {
        var alias = "a";
        var expiresAt = DateTimeOffset.UtcNow;

        var entity = ExpiryIndexEntity.Create(alias, expiresAt);

        Assert.Equal("xx", entity.AliasPartitionKey);
    }

    #endregion
}
