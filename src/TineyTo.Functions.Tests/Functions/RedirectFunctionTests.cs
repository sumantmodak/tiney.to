using Microsoft.Extensions.Logging;
using Moq;
using TineyTo.Functions.Functions;
using TineyTo.Functions.Services;
using TineyTo.Functions.Storage;
using TineyTo.Functions.Storage.Entities;
using TineyTo.Functions.Tests.Mocks;
using Microsoft.AspNetCore.Mvc;

namespace TineyTo.Functions.Tests.Functions;

public class RedirectFunctionTests
{
    private readonly Mock<ILogger<RedirectFunction>> _loggerMock;
    private readonly Mock<IShortUrlRepository> _shortUrlRepoMock;
    private readonly MockTimeProvider _timeProvider;
    private readonly RedirectFunction _function;

    public RedirectFunctionTests()
    {
        _loggerMock = new Mock<ILogger<RedirectFunction>>();
        _shortUrlRepoMock = new Mock<IShortUrlRepository>();
        _timeProvider = new MockTimeProvider();
        _function = new RedirectFunction(
            _loggerMock.Object,
            _shortUrlRepoMock.Object,
            _timeProvider);
    }

    [Fact]
    public async Task Run_ValidAlias_Returns302Redirect()
    {
        // Arrange
        var alias = "abc123";
        var longUrl = "https://example.com/target";
        var entity = ShortUrlEntity.Create(alias, longUrl, _timeProvider.UtcNow);
        
        _shortUrlRepoMock.Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;

        // Act
        var result = await _function.Run(request, alias, CancellationToken.None);

        // Assert
        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Equal(longUrl, redirectResult.Url);
        Assert.False(redirectResult.Permanent);
    }

    [Fact]
    public async Task Run_AliasNotFound_Returns404()
    {
        // Arrange
        var alias = "notfound";
        _shortUrlRepoMock.Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortUrlEntity?)null);

        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;

        // Act
        var result = await _function.Run(request, alias, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Run_ExpiredAlias_Returns410Gone()
    {
        // Arrange
        var alias = "expired";
        var now = DateTimeOffset.UtcNow;
        _timeProvider.UtcNow = now;
        
        var entity = ShortUrlEntity.Create(alias, "https://example.com", now.AddHours(-2), now.AddHours(-1));
        
        _shortUrlRepoMock.Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;

        // Act
        var result = await _function.Run(request, alias, CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(410, statusResult.StatusCode);
    }

    [Fact]
    public async Task Run_DisabledAlias_Returns410Gone()
    {
        // Arrange
        var alias = "disabled";
        var entity = ShortUrlEntity.Create(alias, "https://example.com", _timeProvider.UtcNow);
        entity.IsDisabled = true;
        
        _shortUrlRepoMock.Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;

        // Act
        var result = await _function.Run(request, alias, CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(410, statusResult.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("!invalid!")]
    [InlineData("has spaces")]
    public async Task Run_InvalidAliasFormat_Returns404(string alias)
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;

        // Act
        var result = await _function.Run(request, alias, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
}
