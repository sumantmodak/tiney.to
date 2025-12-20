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

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,32}$")]
    private static partial Regex AliasFormatRegex();

    public RedirectFunction(
        ILogger<RedirectFunction> logger,
        IShortUrlRepository shortUrlRepository,
        ITimeProvider timeProvider)
    {
        _logger = logger;
        _shortUrlRepository = shortUrlRepository;
        _timeProvider = timeProvider;
    }

    [Function("Redirect")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "{alias}")] HttpRequest req,
        string alias,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing redirect for alias: {Alias}", alias);

        // Validate alias format
        if (string.IsNullOrEmpty(alias) || !AliasFormatRegex().IsMatch(alias))
        {
            _logger.LogWarning("Invalid alias format: {Alias}", alias);
            return new NotFoundResult();
        }

        var entity = await _shortUrlRepository.GetByAliasAsync(alias, cancellationToken);

        if (entity == null)
        {
            _logger.LogWarning("Alias not found: {Alias}", alias);
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
}
