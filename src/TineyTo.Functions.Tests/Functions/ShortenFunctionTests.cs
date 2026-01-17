using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TineyTo.Functions.Functions;
using TineyTo.Functions.Models;
using TineyTo.Functions.Services;
using TineyTo.Functions.Storage;
using TineyTo.Functions.Storage.Entities;
using TineyTo.Functions.Tests.Mocks;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;

namespace TineyTo.Functions.Tests.Functions;

public class ShortenFunctionTests
{
    private readonly Mock<ILogger<ShortenFunction>> _loggerMock;
    private readonly Mock<IShortUrlRepository> _shortUrlRepoMock;
    private readonly Mock<IExpiryIndexRepository> _expiryIndexRepoMock;
    private readonly Mock<IUrlIndexRepository> _urlIndexRepoMock;
    private readonly MockAliasGenerator _aliasGenerator;
    private readonly Mock<IUrlValidator> _urlValidatorMock;
    private readonly MockTimeProvider _timeProvider;
    private readonly MockRateLimiter _rateLimiter;
    private readonly MockStatisticsQueue _statisticsQueue;
    private readonly ShortenFunction _function;

    public ShortenFunctionTests()
    {
        _loggerMock = new Mock<ILogger<ShortenFunction>>();
        _shortUrlRepoMock = new Mock<IShortUrlRepository>();
        _expiryIndexRepoMock = new Mock<IExpiryIndexRepository>();
        _urlIndexRepoMock = new Mock<IUrlIndexRepository>();
        _aliasGenerator = new MockAliasGenerator();
        _urlValidatorMock = new Mock<IUrlValidator>();
        _timeProvider = new MockTimeProvider();
        _rateLimiter = new MockRateLimiter();
        _statisticsQueue = new MockStatisticsQueue();

        // Default to valid validation
        _urlValidatorMock.Setup(v => v.ValidateLongUrl(It.IsAny<string>()))
            .Returns((true, (string?)null));
        _urlValidatorMock.Setup(v => v.ValidateExpiresInSeconds(It.IsAny<int?>()))
            .Returns((true, (string?)null));

        // Default to successful insert
        _shortUrlRepoMock.Setup(r => r.InsertAsync(It.IsAny<ShortUrlEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Default to no existing URL (no dedup match)
        _urlIndexRepoMock.Setup(r => r.GetByLongUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UrlIndexEntity?)null);
        _urlIndexRepoMock.Setup(r => r.InsertAsync(It.IsAny<UrlIndexEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var config = new Configuration.ApplicationConfiguration 
        { 
            BaseUrl = "http://localhost:7071",
            AliasLength = 6,
            MaxTtlSeconds = 7776000
        };

        var apiAuthConfig = new Configuration.ApiAuthConfiguration
        {
            IsEnabled = false,
            ValidApiKeys = [],
            AdminApiKeys = []
        };

        _function = new ShortenFunction(
            _loggerMock.Object,
            _shortUrlRepoMock.Object,
            _expiryIndexRepoMock.Object,
            _urlIndexRepoMock.Object,
            _aliasGenerator,
            _urlValidatorMock.Object,
            _timeProvider,
            _rateLimiter,
            _statisticsQueue,
            config,
            apiAuthConfig);
    }

    private static HttpRequest CreateRequest(ShortenRequest body)
    {
        var json = JsonSerializer.Serialize(body);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        
        var context = new DefaultHttpContext();
        context.Request.Body = stream;
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = stream.Length;
        
        return context.Request;
    }

    [Fact]
    public async Task Run_ValidRequest_Returns201Created()
    {
        // Arrange
        var request = new ShortenRequest { LongUrl = "https://example.com" };
        var httpRequest = CreateRequest(request);
        _aliasGenerator.SetNextAlias("abc123");
        _timeProvider.UtcNow = new DateTimeOffset(2025, 12, 20, 10, 0, 0, TimeSpan.Zero);

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        
        var response = Assert.IsType<ShortenResponse>(createdResult.Value);
        Assert.Equal("abc123", response.Alias);
        Assert.Equal("https://example.com", response.LongUrl);
        Assert.Contains("abc123", response.ShortUrl);
    }

    [Fact]
    public async Task Run_InvalidLongUrl_Returns400()
    {
        // Arrange
        var request = new ShortenRequest { LongUrl = "not-a-url" };
        var httpRequest = CreateRequest(request);
        
        _urlValidatorMock.Setup(v => v.ValidateLongUrl("not-a-url"))
            .Returns((false, "longUrl must be a valid absolute URL"));

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task Run_WithExpiry_CreatesExpiryIndex()
    {
        // Arrange
        var now = new DateTimeOffset(2025, 12, 20, 10, 0, 0, TimeSpan.Zero);
        _timeProvider.UtcNow = now;
        
        var request = new ShortenRequest 
        { 
            LongUrl = "https://example.com",
            ExpiresInSeconds = 3600
        };
        var httpRequest = CreateRequest(request);
        _aliasGenerator.SetNextAlias("expiring");

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        Assert.IsType<CreatedResult>(result);
        _expiryIndexRepoMock.Verify(
            r => r.InsertAsync(It.Is<ExpiryIndexEntity>(e => 
                e.AliasRowKey == "expiring" && 
                e.ExpiresAtUtc == now.AddSeconds(3600)), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task Run_WithoutExpiry_CreatesExpiryIndexWithDefaultTtl()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        _timeProvider.UtcNow = now;
        var request = new ShortenRequest { LongUrl = "https://example.com" };
        var httpRequest = CreateRequest(request);
        _aliasGenerator.SetNextAlias("noexpiry");

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        Assert.IsType<CreatedResult>(result);
        // Should create expiry index with default max TTL (7776000 seconds = 90 days)
        _expiryIndexRepoMock.Verify(
            r => r.InsertAsync(It.Is<ExpiryIndexEntity>(e => 
                e.AliasRowKey == "noexpiry" && 
                e.ExpiresAtUtc == now.AddSeconds(7776000)), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task Run_ExistingUrl_ReturnsExistingAlias()
    {
        // Arrange
        var now = new DateTimeOffset(2025, 12, 20, 10, 0, 0, TimeSpan.Zero);
        _timeProvider.UtcNow = now;
        
        var existingIndex = UrlIndexEntity.Create("https://example.com", "existing1", now.AddDays(7));
        _urlIndexRepoMock.Setup(r => r.GetByLongUrlAsync("https://example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIndex);

        var request = new ShortenRequest { LongUrl = "https://example.com" };
        var httpRequest = CreateRequest(request);

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        
        var response = Assert.IsType<ShortenResponse>(okResult.Value);
        Assert.Equal("existing1", response.Alias);
        
        // Should NOT create new short URL
        _shortUrlRepoMock.Verify(r => r.InsertAsync(It.IsAny<ShortUrlEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_ExistingUrlExpired_CreatesNewAlias()
    {
        // Arrange
        var now = new DateTimeOffset(2025, 12, 20, 10, 0, 0, TimeSpan.Zero);
        _timeProvider.UtcNow = now;
        
        // Existing entry expired yesterday
        var expiredIndex = UrlIndexEntity.Create("https://example.com", "expired1", now.AddDays(-1));
        _urlIndexRepoMock.Setup(r => r.GetByLongUrlAsync("https://example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredIndex);

        var request = new ShortenRequest { LongUrl = "https://example.com" };
        var httpRequest = CreateRequest(request);
        _aliasGenerator.SetNextAlias("newalias");

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<ShortenResponse>(createdResult.Value);
        Assert.Equal("newalias", response.Alias);
        
        // Should delete expired index and create new short URL
        _urlIndexRepoMock.Verify(r => r.DeleteAsync("https://example.com", It.IsAny<CancellationToken>()), Times.Once);
        _shortUrlRepoMock.Verify(r => r.InsertAsync(It.IsAny<ShortUrlEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_ExistingUrlNoExpiry_ReturnsExistingAlias()
    {
        // Arrange
        var now = new DateTimeOffset(2025, 12, 20, 10, 0, 0, TimeSpan.Zero);
        _timeProvider.UtcNow = now;
        
        // Existing entry with no expiry (permanent)
        var existingIndex = UrlIndexEntity.Create("https://example.com", "permanent", null);
        _urlIndexRepoMock.Setup(r => r.GetByLongUrlAsync("https://example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIndex);

        var request = new ShortenRequest { LongUrl = "https://example.com" };
        var httpRequest = CreateRequest(request);

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ShortenResponse>(okResult.Value);
        Assert.Equal("permanent", response.Alias);
    }

    [Fact]
    public async Task Run_NewUrl_InsertsUrlIndex()
    {
        // Arrange
        var request = new ShortenRequest { LongUrl = "https://newurl.com" };
        var httpRequest = CreateRequest(request);
        _aliasGenerator.SetNextAlias("newurl1");

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        Assert.IsType<CreatedResult>(result);
        _urlIndexRepoMock.Verify(
            r => r.InsertAsync(It.Is<UrlIndexEntity>(e => 
                e.Alias == "newurl1" && e.LongUrl == "https://newurl.com"), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}

public class ShortenFunctionWithAuthTests
{
    private readonly Mock<ILogger<ShortenFunction>> _loggerMock;
    private readonly Mock<IShortUrlRepository> _shortUrlRepoMock;
    private readonly Mock<IExpiryIndexRepository> _expiryIndexRepoMock;
    private readonly Mock<IUrlIndexRepository> _urlIndexRepoMock;
    private readonly MockAliasGenerator _aliasGenerator;
    private readonly Mock<IUrlValidator> _urlValidatorMock;
    private readonly MockTimeProvider _timeProvider;
    private readonly MockRateLimiter _rateLimiter;
    private readonly MockStatisticsQueue _statisticsQueue;
    private readonly ShortenFunction _function;
    private const string ValidApiKey1 = "test-key-1234567890abcdef";
    private const string ValidApiKey2 = "test-key-9876543210fedcba";

    public ShortenFunctionWithAuthTests()
    {
        _loggerMock = new Mock<ILogger<ShortenFunction>>();
        _shortUrlRepoMock = new Mock<IShortUrlRepository>();
        _expiryIndexRepoMock = new Mock<IExpiryIndexRepository>();
        _urlIndexRepoMock = new Mock<IUrlIndexRepository>();
        _aliasGenerator = new MockAliasGenerator();
        _urlValidatorMock = new Mock<IUrlValidator>();
        _timeProvider = new MockTimeProvider();
        _rateLimiter = new MockRateLimiter();
        _statisticsQueue = new MockStatisticsQueue();

        // Default to valid validation
        _urlValidatorMock.Setup(v => v.ValidateLongUrl(It.IsAny<string>()))
            .Returns((true, (string?)null));
        _urlValidatorMock.Setup(v => v.ValidateExpiresInSeconds(It.IsAny<int?>()))
            .Returns((true, (string?)null));

        // Default to successful insert
        _shortUrlRepoMock.Setup(r => r.InsertAsync(It.IsAny<ShortUrlEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _urlIndexRepoMock.Setup(r => r.GetByLongUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UrlIndexEntity?)null);
        _urlIndexRepoMock.Setup(r => r.InsertAsync(It.IsAny<UrlIndexEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var config = new Configuration.ApplicationConfiguration 
        { 
            BaseUrl = "http://localhost:7071",
            AliasLength = 6,
            MaxTtlSeconds = 7776000
        };

        var apiAuthConfig = new Configuration.ApiAuthConfiguration
        {
            IsEnabled = true,
            ValidApiKeys = new HashSet<string> { ValidApiKey1, ValidApiKey2 },
            AdminApiKeys = new HashSet<string> { "admin-key-abcdef123456" }
        };

        _function = new ShortenFunction(
            _loggerMock.Object,
            _shortUrlRepoMock.Object,
            _expiryIndexRepoMock.Object,
            _urlIndexRepoMock.Object,
            _aliasGenerator,
            _urlValidatorMock.Object,
            _timeProvider,
            _rateLimiter,
            _statisticsQueue,
            config,
            apiAuthConfig);
    }

    private static HttpRequest CreateRequest(ShortenRequest body, string? apiKey = null)
    {
        var json = JsonSerializer.Serialize(body);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        
        var context = new DefaultHttpContext();
        context.Request.Body = stream;
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = stream.Length;

        if (apiKey != null)
        {
            context.Request.Headers["X-API-Key"] = apiKey;
        }
        
        return context.Request;
    }

    [Fact]
    public async Task Run_WithMissingApiKey_Returns401()
    {
        // Arrange
        var request = new ShortenRequest { LongUrl = "https://example.com" };
        var httpRequest = CreateRequest(request); // No API key

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task Run_WithEmptyApiKey_Returns401()
    {
        // Arrange
        var request = new ShortenRequest { LongUrl = "https://example.com" };
        var httpRequest = CreateRequest(request, "");

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task Run_WithInvalidApiKey_Returns401()
    {
        // Arrange
        var request = new ShortenRequest { LongUrl = "https://example.com" };
        var httpRequest = CreateRequest(request, "invalid-key-xyz");

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task Run_WithValidApiKey1_Returns201()
    {
        // Arrange
        var request = new ShortenRequest { LongUrl = "https://example.com" };
        var httpRequest = CreateRequest(request, ValidApiKey1);
        _aliasGenerator.SetNextAlias("abc123");

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
    }

    [Fact]
    public async Task Run_WithValidApiKey2_Returns201()
    {
        // Arrange
        var request = new ShortenRequest { LongUrl = "https://example.com" };
        var httpRequest = CreateRequest(request, ValidApiKey2);
        _aliasGenerator.SetNextAlias("xyz789");

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
    }

    [Fact]
    public async Task Run_WithValidApiKey_ProcessesRequest()
    {
        // Arrange
        var request = new ShortenRequest { LongUrl = "https://example.com" };
        var httpRequest = CreateRequest(request, ValidApiKey1);
        _aliasGenerator.SetNextAlias("test123");

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<ShortenResponse>(createdResult.Value);
        Assert.Equal("http://localhost:7071/test123", response.ShortUrl);
        Assert.Equal("test123", response.Alias);
    }
}
