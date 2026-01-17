using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using TineyTo.Functions.Models;
using TineyTo.Functions.Storage.Entities;

namespace TineyTo.Functions.Services;

/// <summary>
/// Aggregates statistics events in memory before batch writing to Table Storage.
/// </summary>
public class StatisticsAggregator
{
    private readonly ILogger _logger;
    private long _totalLinksCreated = 0;
    private long _totalRedirections = 0;
    private readonly Dictionary<string, long> _perLinkCounts = new();
    private readonly Dictionary<string, DateTimeOffset> _perLinkFirstSeen = new();
    private readonly Dictionary<string, DateTimeOffset> _perLinkLastSeen = new();
    private readonly Dictionary<DateOnly, (long links, long redirects)> _dailyStats = new();

    public StatisticsAggregator(ILogger logger)
    {
        _logger = logger;
    }

    public void Add(StatisticsEvent evt)
    {
        var date = DateOnly.FromDateTime(evt.Timestamp.DateTime);

        if (evt.EventType == StatisticsEventType.LinkCreated)
        {
            _totalLinksCreated++;
            
            var (links, redirects) = _dailyStats.GetValueOrDefault(date);
            _dailyStats[date] = (links + 1, redirects);
        }
        else if (evt.EventType == StatisticsEventType.Redirect)
        {
            _totalRedirections++;
            
            var (links, redirects) = _dailyStats.GetValueOrDefault(date);
            _dailyStats[date] = (links, redirects + 1);

            // Per-link tracking
            _perLinkCounts.TryGetValue(evt.Alias, out var count);
            _perLinkCounts[evt.Alias] = count + 1;

            if (!_perLinkFirstSeen.ContainsKey(evt.Alias))
            {
                _perLinkFirstSeen[evt.Alias] = evt.Timestamp;
            }
            _perLinkLastSeen[evt.Alias] = evt.Timestamp;
        }
    }

    public async Task FlushToStorageAsync(TableClient tableClient, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Flushing statistics: {Links} links, {Redirects} redirects, {AliasCount} aliases, {DayCount} days",
            _totalLinksCreated, _totalRedirections, _perLinkCounts.Count, _dailyStats.Count);

        await UpdateGlobalStatsAsync(tableClient, cancellationToken);
        await UpdatePerLinkStatsAsync(tableClient, cancellationToken);
        await UpdateDailyStatsAsync(tableClient, cancellationToken);
    }

    private async Task UpdateGlobalStatsAsync(TableClient tableClient, CancellationToken cancellationToken)
    {
        if (_totalLinksCreated == 0 && _totalRedirections == 0)
            return;

        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Try to get existing entity
                GlobalStatisticsEntity entity;
                try
                {
                    var response = await tableClient.GetEntityAsync<GlobalStatisticsEntity>(
                        "GlobalStats", "totals", cancellationToken: cancellationToken);
                    entity = response.Value;
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                {
                    // Create new if doesn't exist
                    entity = GlobalStatisticsEntity.Create();
                }

                entity.TotalLinksShortened += _totalLinksCreated;
                entity.TotalRedirections += _totalRedirections;
                entity.LastUpdated = DateTimeOffset.UtcNow;

                await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
                
                _logger.LogDebug("Updated global stats: +{Links} links, +{Redirects} redirects", 
                    _totalLinksCreated, _totalRedirections);
                return;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 412 && attempt < maxRetries - 1)
            {
                // ETag conflict, retry with exponential backoff
                await Task.Delay(100 * (attempt + 1), cancellationToken);
                _logger.LogWarning("Global stats conflict, retrying (attempt {Attempt})", attempt + 1);
            }
        }

        _logger.LogError("Failed to update global stats after {Retries} retries", maxRetries);
    }

    private async Task UpdatePerLinkStatsAsync(TableClient tableClient, CancellationToken cancellationToken)
    {
        foreach (var (alias, count) in _perLinkCounts)
        {
            try
            {
                PerLinkStatisticsEntity entity;
                try
                {
                    var response = await tableClient.GetEntityAsync<PerLinkStatisticsEntity>(
                        alias, "stats", cancellationToken: cancellationToken);
                    entity = response.Value;
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                {
                    entity = PerLinkStatisticsEntity.Create(alias, _perLinkFirstSeen[alias]);
                }

                entity.RedirectionCount += count;
                
                if (_perLinkFirstSeen.TryGetValue(alias, out var firstSeen))
                {
                    if (!entity.FirstRedirect.HasValue || firstSeen < entity.FirstRedirect.Value)
                    {
                        entity.FirstRedirect = firstSeen;
                    }
                }

                if (_perLinkLastSeen.TryGetValue(alias, out var lastSeen))
                {
                    if (!entity.LastRedirect.HasValue || lastSeen > entity.LastRedirect.Value)
                    {
                        entity.LastRedirect = lastSeen;
                    }
                }

                await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
                
                _logger.LogDebug("Updated stats for alias {Alias}: +{Count} redirects", alias, count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update stats for alias {Alias}", alias);
            }
        }
    }

    private async Task UpdateDailyStatsAsync(TableClient tableClient, CancellationToken cancellationToken)
    {
        foreach (var (date, (links, redirects)) in _dailyStats)
        {
            if (links == 0 && redirects == 0)
                continue;

            try
            {
                DailyStatisticsEntity entity;
                var rowKey = date.ToString("yyyy-MM-dd");
                
                try
                {
                    var response = await tableClient.GetEntityAsync<DailyStatisticsEntity>(
                        "DailyStats", rowKey, cancellationToken: cancellationToken);
                    entity = response.Value;
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                {
                    entity = DailyStatisticsEntity.Create(date);
                }

                entity.LinksCreated += links;
                entity.Redirections += redirects;

                await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
                
                _logger.LogDebug("Updated daily stats for {Date}: +{Links} links, +{Redirects} redirects", 
                    date, links, redirects);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update daily stats for {Date}", date);
            }
        }
    }
}
