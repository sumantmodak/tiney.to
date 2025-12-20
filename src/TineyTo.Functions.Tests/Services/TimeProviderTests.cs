using TineyTo.Functions.Services;

namespace TineyTo.Functions.Tests.Services;

public class TimeProviderTests
{
    [Fact]
    public void UtcNow_ReturnsCurrentUtcTime()
    {
        // Arrange
        var timeProvider = new SystemTimeProvider();
        var before = DateTimeOffset.UtcNow;

        // Act
        var result = timeProvider.UtcNow;

        var after = DateTimeOffset.UtcNow;

        // Assert
        Assert.True(result >= before && result <= after);
        Assert.Equal(TimeSpan.Zero, result.Offset);
    }
}
