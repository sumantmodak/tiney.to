using Microsoft.Extensions.Logging;

namespace TineyTo.Functions.Services;

/// <summary>
/// Implementation of ICacheMetrics that logs cache operations using ILogger.
/// </summary>
public class LoggingCacheMetrics : ICacheMetrics
{
    private readonly ILogger<LoggingCacheMetrics> _logger;

    public LoggingCacheMetrics(ILogger<LoggingCacheMetrics> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void RecordHit(string operation, string? alias = null, string? longUrl = null)
    {
        if (!string.IsNullOrEmpty(alias) && !string.IsNullOrEmpty(longUrl))
        {
            _logger.LogInformation("Cache HIT: {Operation}, Alias: {Alias}, LongUrl: {LongUrl}", operation, alias, longUrl);
        }
        else
        {
            _logger.LogInformation("Cache HIT: {Operation}", operation);
        }
    }

    public void RecordMiss(string operation, string? alias = null)
    {
        if (!string.IsNullOrEmpty(alias))
        {
            _logger.LogInformation("Cache MISS: {Operation}, Alias: {Alias}", operation, alias);
        }
        else
        {
            _logger.LogInformation("Cache MISS: {Operation}", operation);
        }
    }

    public void RecordEviction(string key, string? alias = null)
    {
        if (!string.IsNullOrEmpty(alias))
        {
            _logger.LogInformation("Cache EVICTION: {Key}, Alias: {Alias}", key, alias);
        }
        else
        {
            _logger.LogInformation("Cache EVICTION: {Key}", key);
        }
    }
}
