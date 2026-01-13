using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using TineyTo.Functions.Configuration;
using TineyTo.Functions.Services;
using TineyTo.Functions.Tests.Mocks;

namespace TineyTo.Functions.Tests.Services;

public class SlidingWindowRateLimiterTests
{
    private readonly IMemoryCache _cache;
    private readonly MockTimeProvider _timeProvider;
    private readonly Mock<ILogger<SlidingWindowRateLimiter>> _loggerMock;
    private readonly RateLimitConfiguration _config;

    public SlidingWindowRateLimiterTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _timeProvider = new MockTimeProvider();
        _loggerMock = new Mock<ILogger<SlidingWindowRateLimiter>>();
        _config = new RateLimitConfiguration
        {
            IsEnabled = true,
            ShortenPerUrlLimit = 5,
            ShortenPerUrlWindowSeconds = 60,
            ShortenPerIpLimit = 10,
            ShortenPerIpWindowSeconds = 60,
            RedirectPerAliasLimit = 100,
            RedirectPerAliasWindowSeconds = 10,
            RedirectPerIpLimit = 60,
            RedirectPerIpWindowSeconds = 60,
            NotFoundPerIpLimit = 20,
            NotFoundPerIpWindowSeconds = 60
        };
    }

    private SlidingWindowRateLimiter CreateRateLimiter()
    {
        return new SlidingWindowRateLimiter(_cache, _config, _timeProvider, _loggerMock.Object);
    }

    [Fact]
    public void CheckShortenUrl_WithinLimit_ReturnsAllowed()
    {
        var limiter = CreateRateLimiter();

        for (int i = 0; i < 5; i++)
        {
            var result = limiter.CheckShortenUrl("https://example.com/test");
            Assert.True(result.IsAllowed);
            Assert.Equal(i + 1, result.CurrentCount);
        }
    }

    [Fact]
    public void CheckShortenUrl_ExceedsLimit_ReturnsBlocked()
    {
        var limiter = CreateRateLimiter();

        // Use up the limit
        for (int i = 0; i < 5; i++)
        {
            var result = limiter.CheckShortenUrl("https://example.com/test");
            Assert.True(result.IsAllowed);
        }

        // Next request should be blocked
        var blockedResult = limiter.CheckShortenUrl("https://example.com/test");
        Assert.False(blockedResult.IsAllowed);
        Assert.Equal(6, blockedResult.CurrentCount);
        Assert.Equal(5, blockedResult.Limit);
        Assert.True(blockedResult.RetryAfterSeconds > 0);
    }

    [Fact]
    public void CheckShortenUrl_DifferentUrls_IndependentLimits()
    {
        var limiter = CreateRateLimiter();

        // Use up limit for URL 1
        for (int i = 0; i < 5; i++)
        {
            limiter.CheckShortenUrl("https://example.com/url1");
        }

        // URL 2 should still be allowed
        var result = limiter.CheckShortenUrl("https://example.com/url2");
        Assert.True(result.IsAllowed);
        Assert.Equal(1, result.CurrentCount);
    }

    [Fact]
    public void CheckShortenIp_WithinLimit_ReturnsAllowed()
    {
        var limiter = CreateRateLimiter();

        for (int i = 0; i < 10; i++)
        {
            var result = limiter.CheckShortenIp("192.168.1.1");
            Assert.True(result.IsAllowed);
        }
    }

    [Fact]
    public void CheckShortenIp_ExceedsLimit_ReturnsBlocked()
    {
        var limiter = CreateRateLimiter();

        for (int i = 0; i < 10; i++)
        {
            limiter.CheckShortenIp("192.168.1.1");
        }

        var result = limiter.CheckShortenIp("192.168.1.1");
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void CheckRedirectAlias_WithinLimit_ReturnsAllowed()
    {
        var limiter = CreateRateLimiter();

        for (int i = 0; i < 100; i++)
        {
            var result = limiter.CheckRedirectAlias("abc123");
            Assert.True(result.IsAllowed);
        }
    }

    [Fact]
    public void CheckRedirectAlias_ExceedsLimit_ReturnsBlocked()
    {
        var limiter = CreateRateLimiter();

        for (int i = 0; i < 100; i++)
        {
            limiter.CheckRedirectAlias("abc123");
        }

        var result = limiter.CheckRedirectAlias("abc123");
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void CheckRedirectIp_WithinLimit_ReturnsAllowed()
    {
        var limiter = CreateRateLimiter();

        for (int i = 0; i < 60; i++)
        {
            var result = limiter.CheckRedirectIp("192.168.1.1");
            Assert.True(result.IsAllowed);
        }
    }

    [Fact]
    public void RecordNotFound_WithinLimit_ReturnsAllowed()
    {
        var limiter = CreateRateLimiter();

        for (int i = 0; i < 20; i++)
        {
            var result = limiter.RecordNotFound("192.168.1.1");
            Assert.True(result.IsAllowed);
        }
    }

    [Fact]
    public void RecordNotFound_ExceedsLimit_ReturnsBlocked()
    {
        var limiter = CreateRateLimiter();

        for (int i = 0; i < 20; i++)
        {
            limiter.RecordNotFound("192.168.1.1");
        }

        var result = limiter.RecordNotFound("192.168.1.1");
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void CheckAll_AllAllowed_ReturnsAllowed()
    {
        var limiter = CreateRateLimiter();

        var result = limiter.CheckAll(
            () => limiter.CheckShortenIp("192.168.1.1"),
            () => limiter.CheckShortenUrl("https://example.com")
        );

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public void CheckAll_OneBlocked_ReturnsBlocked()
    {
        var limiter = CreateRateLimiter();

        // Exhaust URL limit
        for (int i = 0; i <= 5; i++)
        {
            limiter.CheckShortenUrl("https://example.com");
        }

        var result = limiter.CheckAll(
            () => limiter.CheckShortenIp("192.168.1.1"),
            () => limiter.CheckShortenUrl("https://example.com")
        );

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void RateLimiter_WhenDisabled_AlwaysAllows()
    {
        var disabledConfig = new RateLimitConfiguration
        {
            IsEnabled = false,
            ShortenPerUrlLimit = 1,
            ShortenPerIpLimit = 1,
            RedirectPerAliasLimit = 1,
            RedirectPerIpLimit = 1,
            NotFoundPerIpLimit = 1
        };

        var limiter = new SlidingWindowRateLimiter(_cache, disabledConfig, _timeProvider, _loggerMock.Object);

        // Should always be allowed even after many requests
        for (int i = 0; i < 100; i++)
        {
            Assert.True(limiter.CheckShortenUrl("https://example.com").IsAllowed);
            Assert.True(limiter.CheckShortenIp("192.168.1.1").IsAllowed);
            Assert.True(limiter.CheckRedirectAlias("abc").IsAllowed);
            Assert.True(limiter.CheckRedirectIp("192.168.1.1").IsAllowed);
            Assert.True(limiter.RecordNotFound("192.168.1.1").IsAllowed);
        }
    }

    [Fact]
    public void RateLimiter_WindowExpires_CountResets()
    {
        var limiter = CreateRateLimiter();
        var startTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        _timeProvider.UtcNow = startTime;

        // Use up the limit
        for (int i = 0; i < 5; i++)
        {
            limiter.CheckShortenUrl("https://example.com");
        }

        // Should be blocked
        Assert.False(limiter.CheckShortenUrl("https://example.com").IsAllowed);

        // Advance time past the window (60 seconds)
        _timeProvider.UtcNow = startTime.AddSeconds(61);

        // Should be allowed again
        var result = limiter.CheckShortenUrl("https://example.com");
        Assert.True(result.IsAllowed);
        Assert.Equal(1, result.CurrentCount);
    }
}
