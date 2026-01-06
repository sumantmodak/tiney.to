using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TineyTo.Functions.Services;
using TineyTo.Functions.Storage;

namespace TineyTo.Functions.Functions;

public partial class RedirectFunction
{
    private readonly ILogger<RedirectFunction> _logger;
    private readonly IShortUrlRepository _shortUrlRepository;
    private readonly ITimeProvider _timeProvider;
    private readonly IRateLimiter _rateLimiter;

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,32}$")]
    private static partial Regex AliasFormatRegex();

    public RedirectFunction(
        ILogger<RedirectFunction> logger,
        IShortUrlRepository shortUrlRepository,
        ITimeProvider timeProvider,
        IRateLimiter rateLimiter)
    {
        _logger = logger;
        _shortUrlRepository = shortUrlRepository;
        _timeProvider = timeProvider;
        _rateLimiter = rateLimiter;
    }

    [Function("Redirect")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{alias?}")] HttpRequest req,
        string? alias,
        CancellationToken cancellationToken)
    {
        // Handle root path - redirect to www.tiney.to
        if (string.IsNullOrEmpty(alias))
        {
            _logger.LogInformation("Root path accessed, redirecting to www.tiney.to");
            return new RedirectResult("https://www.tiney.to", permanent: true);
        }

        _logger.LogInformation("Processing redirect for alias: {Alias}", alias);

        // Extract client IP for rate limiting
        var clientIp = ClientIpExtractor.GetClientIp(req);

        // Check IP-based rate limit first
        var ipRateResult = _rateLimiter.CheckRedirectIp(clientIp);
        if (!ipRateResult.IsAllowed)
        {
            _logger.LogWarning("Redirect rate limit exceeded for IP {ClientIp}: {Count}/{Limit}", 
                clientIp, ipRateResult.CurrentCount, ipRateResult.Limit);
            return CreateRateLimitResponse(ipRateResult);
        }

        // Validate alias format
        if (!AliasFormatRegex().IsMatch(alias))
        {
            _logger.LogWarning("Invalid alias format: {Alias}", alias);
            // Record 404 for potential scanning detection
            var notFoundResult = _rateLimiter.RecordNotFound(clientIp);
            if (!notFoundResult.IsAllowed)
            {
                _logger.LogWarning("404 rate limit exceeded for IP {ClientIp} (possible scanning)", clientIp);
                return CreateRateLimitResponse(notFoundResult);
            }
            return new NotFoundResult();
        }

        // Check alias-based rate limit (hotlink protection)
        var aliasRateResult = _rateLimiter.CheckRedirectAlias(alias);
        if (!aliasRateResult.IsAllowed)
        {
            _logger.LogWarning("Redirect rate limit exceeded for alias {Alias}: {Count}/{Limit}", 
                alias, aliasRateResult.CurrentCount, aliasRateResult.Limit);
            return CreateRateLimitResponse(aliasRateResult);
        }

        var entity = await _shortUrlRepository.GetByAliasAsync(alias, cancellationToken);

        if (entity == null)
        {
            _logger.LogWarning("Alias not found: {Alias}", alias);
            // Record 404 for potential scanning detection
            var notFoundResult = _rateLimiter.RecordNotFound(clientIp);
            if (!notFoundResult.IsAllowed)
            {
                _logger.LogWarning("404 rate limit exceeded for IP {ClientIp} (possible scanning)", clientIp);
                return CreateRateLimitResponse(notFoundResult);
            }
            return new NotFoundResult();
        }

        // Check if expired or disabled
        var now = _timeProvider.UtcNow;
        if (entity.IsDisabled || entity.IsExpired(now))
        {
            _logger.LogWarning("Alias expired or disabled: {Alias}", alias);
            return new StatusCodeResult(StatusCodes.Status410Gone);
        }

        _logger.LogInformation("Redirecting {Alias} to {LongUrl}", alias, entity.LongUrl);
        return new RedirectResult(entity.LongUrl, permanent: false);
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
