using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
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
    private readonly IAliasGenerator _aliasGenerator;
    private readonly IUrlValidator _urlValidator;
    private readonly ITimeProvider _timeProvider;
    private readonly string _shortBaseUrl;
    private const int MaxAliasRetries = 10;

    [GeneratedRegex(@"^[A-Za-z0-9_-]{3,32}$")]
    private static partial Regex AliasFormatRegex();

    public ShortenFunction(
        ILogger<ShortenFunction> logger,
        IShortUrlRepository shortUrlRepository,
        IExpiryIndexRepository expiryIndexRepository,
        IAliasGenerator aliasGenerator,
        IUrlValidator urlValidator,
        ITimeProvider timeProvider)
    {
        _logger = logger;
        _shortUrlRepository = shortUrlRepository;
        _expiryIndexRepository = expiryIndexRepository;
        _aliasGenerator = aliasGenerator;
        _urlValidator = urlValidator;
        _timeProvider = timeProvider;
        _shortBaseUrl = Environment.GetEnvironmentVariable("SHORT_BASE_URL") ?? "http://localhost:7071";
    }

    [Function("Shorten")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "shorten")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing shorten request");

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

        // Validate custom alias
        var (aliasValid, aliasError) = _urlValidator.ValidateCustomAlias(request.CustomAlias);
        if (!aliasValid)
        {
            return new BadRequestObjectResult(new { error = aliasError });
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
            : null;

        string alias;
        ShortUrlEntity entity;

        // Handle custom alias vs random alias
        if (!string.IsNullOrEmpty(request.CustomAlias))
        {
            alias = request.CustomAlias;
            entity = ShortUrlEntity.Create(alias, request.LongUrl, createdAtUtc, expiresAtUtc);

            var inserted = await _shortUrlRepository.InsertAsync(entity, cancellationToken);
            if (!inserted)
            {
                _logger.LogWarning("Custom alias {Alias} already exists", alias);
                return new ConflictObjectResult(new { error = "Alias already taken" });
            }
        }
        else
        {
            // Random alias with retry loop
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

        _logger.LogInformation("Created short URL: {Alias} -> {LongUrl}", alias, request.LongUrl);

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
}
