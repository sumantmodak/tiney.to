using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TineyTo.Functions.Configuration;
using TineyTo.Functions.Models;
using TineyTo.Functions.Services;
using TineyTo.Functions.Storage;
using TineyTo.Functions.Storage.Entities;

namespace TineyTo.Functions.Functions;

public partial class ShortenFunction
{
    private readonly ILogger<ShortenFunction> _logger;
    private readonly IShortUrlRepository _shortUrlRepository;
    private readonly IExpiryIndexRepository _expiryIndexRepository;
    private readonly IUrlIndexRepository _urlIndexRepository;
    private readonly IAliasGenerator _aliasGenerator;
    private readonly IUrlValidator _urlValidator;
    private readonly ITimeProvider _timeProvider;
    private readonly IRateLimiter _rateLimiter;
    private readonly IStatisticsQueue _statisticsQueue;
    private readonly ApiAuthConfiguration _apiAuthConfig;
    private readonly string _shortBaseUrl;
    private const int MaxAliasRetries = 3;
    private readonly int _maxTtlSeconds;

    [GeneratedRegex(@"^[A-Za-z0-9_-]{3,32}$")]
    private static partial Regex AliasFormatRegex();

    public ShortenFunction(
        ILogger<ShortenFunction> logger,
        IShortUrlRepository shortUrlRepository,
        IExpiryIndexRepository expiryIndexRepository,
        IUrlIndexRepository urlIndexRepository,
        IAliasGenerator aliasGenerator,
        IUrlValidator urlValidator,
        ITimeProvider timeProvider,
        IRateLimiter rateLimiter,
        IStatisticsQueue statisticsQueue,
        ApplicationConfiguration config,
        ApiAuthConfiguration apiAuthConfig)
    {
        _logger = logger;
        _shortUrlRepository = shortUrlRepository;
        _expiryIndexRepository = expiryIndexRepository;
        _urlIndexRepository = urlIndexRepository;
        _aliasGenerator = aliasGenerator;
        _urlValidator = urlValidator;
        _timeProvider = timeProvider;
        _rateLimiter = rateLimiter;
        _statisticsQueue = statisticsQueue;
        _apiAuthConfig = apiAuthConfig;
        _shortBaseUrl = config.BaseUrl;
        _maxTtlSeconds = config.MaxTtlSeconds;
    }

    [Function("Shorten")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/shorten")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing shorten request");

        // Validate API authentication if enabled
        if (_apiAuthConfig.IsEnabled)
        {
            if (!req.Headers.TryGetValue("X-API-Key", out var apiKeyHeader) || 
                string.IsNullOrWhiteSpace(apiKeyHeader))
            {
                _logger.LogWarning("Shorten request rejected: Missing or empty X-API-Key header");
                return new UnauthorizedObjectResult(new { error = "API key required" });
            }

            var providedKey = apiKeyHeader.ToString();
            if (!_apiAuthConfig.ValidApiKeys.Contains(providedKey))
            {
                _logger.LogWarning("Shorten request rejected: Invalid API key provided");
                return new UnauthorizedObjectResult(new { error = "Invalid API key" });
            }

            _logger.LogDebug("API authentication successful");
        }

        // Extract client IP for rate limiting
        var clientIp = ClientIpExtractor.GetClientIp(req);

        // Check IP-based rate limit first (before parsing body)
        var ipRateResult = _rateLimiter.CheckShortenIp(clientIp);
        if (!ipRateResult.IsAllowed)
        {
            _logger.LogWarning("Rate limit exceeded for IP {ClientIp}: {Count}/{Limit}", 
                clientIp, ipRateResult.CurrentCount, ipRateResult.Limit);
            return CreateRateLimitResponse(ipRateResult);
        }
        
        ShortenRequest? request;
        try
        {
            request = await req.ReadFromJsonAsync<ShortenRequest>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse request body");
            return new BadRequestObjectResult(new { error = "Invalid request body" });
        }   

        if (request == null)
        {
            return new BadRequestObjectResult(new { error = "Request body is required" });
        }

        // Validate long URL
        var (longUrlValid, longUrlError) = _urlValidator.ValidateLongUrl(request.LongUrl);
        if (!longUrlValid)
        {
            return new BadRequestObjectResult(new { error = longUrlError });
        }

        // Check URL-based rate limit (after validation to avoid wasting resources)
        var urlRateResult = _rateLimiter.CheckShortenUrl(request.LongUrl);
        if (!urlRateResult.IsAllowed)
        {
            _logger.LogWarning("Rate limit exceeded for URL from {ClientIp}: {Count}/{Limit}", 
                clientIp, urlRateResult.CurrentCount, urlRateResult.Limit);
            return CreateRateLimitResponse(urlRateResult);
        }

        // Validate TTL
        var (ttlValid, ttlError) = _urlValidator.ValidateExpiresInSeconds(request.ExpiresInSeconds);
        if (!ttlValid)
        {
            return new BadRequestObjectResult(new { error = ttlError });
        }

        var createdAtUtc = _timeProvider.UtcNow;
        DateTimeOffset? expiresAtUtc = request.ExpiresInSeconds.HasValue
            ? createdAtUtc.AddSeconds(request.ExpiresInSeconds.Value)
            : createdAtUtc.AddSeconds(_maxTtlSeconds);

        // Check if URL already exists (deduplication)
        var existingIndex = await _urlIndexRepository.GetByLongUrlAsync(request.LongUrl, cancellationToken);
        if (existingIndex != null)
        {
            // Check if the existing short URL is still valid (not expired)
            if (!existingIndex.ExpiresAtUtc.HasValue || existingIndex.ExpiresAtUtc.Value > createdAtUtc)
            {
                _logger.LogInformation("Returning existing short URL for: {LongUrl} -> {Alias}", request.LongUrl, existingIndex.Alias);
                
                var existingResponse = new ShortenResponse
                {
                    Alias = existingIndex.Alias,
                    ShortUrl = $"{_shortBaseUrl.TrimEnd('/')}/{existingIndex.Alias}",
                    LongUrl = request.LongUrl,
                    CreatedAtUtc = createdAtUtc,
                    ExpiresAtUtc = existingIndex.ExpiresAtUtc
                };

                // Return 200 OK for existing URL (not 201 Created)
                return new OkObjectResult(existingResponse);
            }
            else
            {
                // Expired - clean up the stale index entry
                _logger.LogDebug("Existing URL index expired, creating new entry");
                await _urlIndexRepository.DeleteAsync(request.LongUrl, cancellationToken);
            }
        }

        // Generate random alias with retry loop
        string alias;
        ShortUrlEntity entity;
        var retries = 0;
        bool inserted;
        do
        {
            alias = _aliasGenerator.Generate();
            entity = ShortUrlEntity.Create(alias, request.LongUrl, createdAtUtc, expiresAtUtc);
            inserted = await _shortUrlRepository.InsertAsync(entity, cancellationToken);
            retries++;
        } while (!inserted && retries < MaxAliasRetries);

        if (!inserted)
        {
            _logger.LogError("Failed to generate unique alias after {Retries} attempts", MaxAliasRetries);
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }

        // Insert expiry index if expiring
        if (expiresAtUtc.HasValue)
        {
            try
            {
                var expiryEntity = ExpiryIndexEntity.Create(alias, expiresAtUtc.Value);
                await _expiryIndexRepository.InsertAsync(expiryEntity, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to insert expiry index for alias {Alias}, rolling back", alias);
                await _shortUrlRepository.DeleteAsync(alias, cancellationToken);
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }

        // Insert URL index for deduplication (best effort - don't fail if this fails)
        try
        {
            var urlIndexEntity = UrlIndexEntity.Create(request.LongUrl, alias, expiresAtUtc);
            await _urlIndexRepository.InsertAsync(urlIndexEntity, cancellationToken);
        }
        catch (Exception ex)
        {
            // Log but don't fail - dedup is optimization, not critical
            _logger.LogWarning(ex, "Failed to insert URL index for alias {Alias}", alias);
        }

        _logger.LogInformation("Created short URL: {Alias} -> {LongUrl}", alias, request.LongUrl);

        // Queue statistics event (fire-and-forget, don't wait)
        _ = _statisticsQueue.QueueEventAsync(new StatisticsEvent
        {
            EventType = StatisticsEventType.LinkCreated,
            Timestamp = _timeProvider.UtcNow,
            Alias = alias
        }, cancellationToken);

        var response = new ShortenResponse
        {
            Alias = alias,
            ShortUrl = $"{_shortBaseUrl.TrimEnd('/')}/{alias}",
            LongUrl = request.LongUrl,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc
        };

        return new CreatedResult($"/{alias}", response);
    }

    /// <summary>
    /// Creates a 429 Too Many Requests response with appropriate headers.
    /// </summary>
    private static IActionResult CreateRateLimitResponse(RateLimitResult result)
    {
        var response = new ObjectResult(new
        {
            error = "Too many requests. Please try again later.",
            retryAfterSeconds = result.RetryAfterSeconds
        })
        {
            StatusCode = StatusCodes.Status429TooManyRequests
        };
        return response;
    }
}
