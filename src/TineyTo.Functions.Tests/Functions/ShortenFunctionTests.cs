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
    private readonly MockAliasGenerator _aliasGenerator;
    private readonly Mock<IUrlValidator> _urlValidatorMock;
    private readonly MockTimeProvider _timeProvider;
    private readonly ShortenFunction _function;

    public ShortenFunctionTests()
    {
        _loggerMock = new Mock<ILogger<ShortenFunction>>();
        _shortUrlRepoMock = new Mock<IShortUrlRepository>();
        _expiryIndexRepoMock = new Mock<IExpiryIndexRepository>();
        _aliasGenerator = new MockAliasGenerator();
        _urlValidatorMock = new Mock<IUrlValidator>();
        _timeProvider = new MockTimeProvider();

        // Default to valid validation
        _urlValidatorMock.Setup(v => v.ValidateLongUrl(It.IsAny<string>()))
            .Returns((true, (string?)null));
        _urlValidatorMock.Setup(v => v.ValidateCustomAlias(It.IsAny<string?>()))
            .Returns((true, (string?)null));
        _urlValidatorMock.Setup(v => v.ValidateExpiresInSeconds(It.IsAny<int?>()))
            .Returns((true, (string?)null));

        // Default to successful insert
        _shortUrlRepoMock.Setup(r => r.InsertAsync(It.IsAny<ShortUrlEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _function = new ShortenFunction(
            _loggerMock.Object,
            _shortUrlRepoMock.Object,
            _expiryIndexRepoMock.Object,
            _aliasGenerator,
            _urlValidatorMock.Object,
            _timeProvider);
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
    public async Task Run_CustomAliasConflict_Returns409()
    {
        // Arrange
        var request = new ShortenRequest 
        { 
            LongUrl = "https://example.com",
            CustomAlias = "taken"
        };
        var httpRequest = CreateRequest(request);
        
        _shortUrlRepoMock.Setup(r => r.InsertAsync(It.IsAny<ShortUrlEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Conflict

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflictResult.StatusCode);
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
    public async Task Run_WithoutExpiry_DoesNotCreateExpiryIndex()
    {
        // Arrange
        var request = new ShortenRequest { LongUrl = "https://example.com" };
        var httpRequest = CreateRequest(request);
        _aliasGenerator.SetNextAlias("noexpiry");

        // Act
        var result = await _function.Run(httpRequest, CancellationToken.None);

        // Assert
        Assert.IsType<CreatedResult>(result);
        _expiryIndexRepoMock.Verify(
            r => r.InsertAsync(It.IsAny<ExpiryIndexEntity>(), It.IsAny<CancellationToken>()), 
            Times.Never);
    }
}
