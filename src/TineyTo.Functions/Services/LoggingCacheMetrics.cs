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

    public void RecordHit(string operation)
    {
        _logger.LogDebug("Cache HIT: {Operation}", operation);
    }

    public void RecordMiss(string operation)
    {
        _logger.LogDebug("Cache MISS: {Operation}", operation);
    }

    public void RecordEviction(string key)
    {
        _logger.LogDebug("Cache EVICTION: {Key}", key);
    }
}
