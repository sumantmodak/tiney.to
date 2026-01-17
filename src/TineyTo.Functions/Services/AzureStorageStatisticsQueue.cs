using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging;
using TineyTo.Functions.Models;

namespace TineyTo.Functions.Services;

/// <summary>
/// Azure Storage Queue implementation of statistics event queue.
/// </summary>
public class AzureStorageStatisticsQueue : IStatisticsQueue
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<AzureStorageStatisticsQueue> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AzureStorageStatisticsQueue(
        QueueClient queueClient,
        ILogger<AzureStorageStatisticsQueue> logger)
    {
        _queueClient = queueClient;
        _logger = logger;
    }

    public async Task QueueEventAsync(StatisticsEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            var messageJson = JsonSerializer.Serialize(@event, JsonOptions);
            await _queueClient.SendMessageAsync(messageJson, cancellationToken);
            
            _logger.LogDebug(
                "Queued statistics event: {EventType} for alias {Alias}", 
                @event.EventType, 
                @event.Alias);
        }
        catch (Exception ex)
        {
            // Log but don't fail the request - statistics are non-critical
            _logger.LogWarning(
                ex, 
                "Failed to queue statistics event: {EventType} for alias {Alias}", 
                @event.EventType, 
                @event.Alias);
        }
    }
}
