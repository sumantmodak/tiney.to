using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TineyTo.Functions.Configuration;
using TineyTo.Functions.Functions;
using TineyTo.Functions.Models;
using TineyTo.Functions.Services;
using TineyTo.Functions.Storage;
using TineyTo.Functions.Storage.Entities;
using TineyTo.Functions.Tests.Mocks;

namespace TineyTo.Functions.Tests.Functions;

public class StatisticsQueueIntegrationTests
{
    [Fact]
    public async Task ShortenFunction_SuccessfulCreation_QueuesLinkCreatedEvent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<ShortenFunction>>();
        var shortUrlRepoMock = new Mock<IShortUrlRepository>();
        var expiryIndexRepoMock = new Mock<IExpiryIndexRepository>();
        var urlIndexRepoMock = new Mock<IUrlIndexRepository>();
        var aliasGenerator = new MockAliasGenerator();
        var urlValidatorMock = new Mock<IUrlValidator>();
        var timeProvider = new MockTimeProvider();
        var rateLimiter = new MockRateLimiter();
        var statisticsQueue = new MockStatisticsQueue();

        urlValidatorMock.Setup(v => v.ValidateLongUrl(It.IsAny<string>()))
            .Returns((true, (string?)null));
        urlValidatorMock.Setup(v => v.ValidateExpiresInSeconds(It.IsAny<int?>()))
            .Returns((true, (string?)null));
        shortUrlRepoMock.Setup(r => r.InsertAsync(It.IsAny<ShortUrlEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        urlIndexRepoMock.Setup(r => r.GetByLongUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UrlIndexEntity?)null);
        urlIndexRepoMock.Setup(r => r.InsertAsync(It.IsAny<UrlIndexEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var config = new ApplicationConfiguration
        {
            BaseUrl = "http://localhost:7071",
            AliasLength = 6,
            MaxTtlSeconds = 7776000
        };

        var apiAuthConfig = new ApiAuthConfiguration
        {
            IsEnabled = false,
            ValidApiKeys = [],
            AdminApiKeys = []
        };

        var function = new ShortenFunction(
            loggerMock.Object,
            shortUrlRepoMock.Object,
            expiryIndexRepoMock.Object,
            urlIndexRepoMock.Object,
            aliasGenerator,
            urlValidatorMock.Object,
            timeProvider,
            rateLimiter,
            statisticsQueue,
            config,
            apiAuthConfig);

        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;
        request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
            "{\"longUrl\":\"https://example.com/test\"}"));
        request.ContentType = "application/json";

        // Act
        var result = await function.Run(request, CancellationToken.None);

        // Assert
        Assert.IsType<CreatedResult>(result);
        Assert.Single(statisticsQueue.QueuedEvents);
        
        var queuedEvent = statisticsQueue.QueuedEvents[0];
        Assert.Equal(StatisticsEventType.LinkCreated, queuedEvent.EventType);
        Assert.Equal("random", queuedEvent.Alias); // Default alias from MockAliasGenerator
        Assert.Equal(timeProvider.UtcNow, queuedEvent.Timestamp);
    }

    [Fact]
    public async Task RedirectFunction_SuccessfulRedirect_QueuesRedirectEvent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RedirectFunction>>();
        var shortUrlRepoMock = new Mock<IShortUrlRepository>();
        var timeProvider = new MockTimeProvider();
        var rateLimiter = new MockRateLimiter();
        var statisticsQueue = new MockStatisticsQueue();

        var alias = "abc123";
        var longUrl = "https://example.com/target";
        var entity = ShortUrlEntity.Create(alias, longUrl, timeProvider.UtcNow);

        shortUrlRepoMock.Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var function = new RedirectFunction(
            loggerMock.Object,
            shortUrlRepoMock.Object,
            timeProvider,
            rateLimiter,
            statisticsQueue);

        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;

        // Act
        var result = await function.Run(request, alias, CancellationToken.None);

        // Assert
        Assert.IsType<RedirectResult>(result);
        Assert.Single(statisticsQueue.QueuedEvents);
        
        var queuedEvent = statisticsQueue.QueuedEvents[0];
        Assert.Equal(StatisticsEventType.Redirect, queuedEvent.EventType);
        Assert.Equal(alias, queuedEvent.Alias);
        Assert.Equal(timeProvider.UtcNow, queuedEvent.Timestamp);
    }

    [Fact]
    public async Task RedirectFunction_ExpiredLink_DoesNotQueueEvent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RedirectFunction>>();
        var shortUrlRepoMock = new Mock<IShortUrlRepository>();
        var timeProvider = new MockTimeProvider();
        var rateLimiter = new MockRateLimiter();
        var statisticsQueue = new MockStatisticsQueue();

        var alias = "expired";
        var longUrl = "https://example.com/target";
        var expiredTime = timeProvider.UtcNow.AddDays(-1);
        var entity = ShortUrlEntity.Create(alias, longUrl, timeProvider.UtcNow.AddDays(-2), expiredTime);

        shortUrlRepoMock.Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var function = new RedirectFunction(
            loggerMock.Object,
            shortUrlRepoMock.Object,
            timeProvider,
            rateLimiter,
            statisticsQueue);

        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;

        // Act
        var result = await function.Run(request, alias, CancellationToken.None);

        // Assert
        Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status410Gone, ((StatusCodeResult)result).StatusCode);
        Assert.Empty(statisticsQueue.QueuedEvents); // No event queued for expired links
    }

    [Fact]
    public async Task RedirectFunction_NotFound_DoesNotQueueEvent()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<RedirectFunction>>();
        var shortUrlRepoMock = new Mock<IShortUrlRepository>();
        var timeProvider = new MockTimeProvider();
        var rateLimiter = new MockRateLimiter();
        var statisticsQueue = new MockStatisticsQueue();

        var alias = "notfound";

        shortUrlRepoMock.Setup(r => r.GetByAliasAsync(alias, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ShortUrlEntity?)null);

        var function = new RedirectFunction(
            loggerMock.Object,
            shortUrlRepoMock.Object,
            timeProvider,
            rateLimiter,
            statisticsQueue);

        var httpContext = new DefaultHttpContext();
        var request = httpContext.Request;

        // Act
        var result = await function.Run(request, alias, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(statisticsQueue.QueuedEvents); // No event queued for 404s
    }
}
