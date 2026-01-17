using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TineyTo.Functions.Models;
using TineyTo.Functions.Services;

namespace TineyTo.Functions.Functions;

public class StatisticsProcessorFunction
{
    private readonly ILogger<StatisticsProcessorFunction> _logger;
    private readonly QueueClient _statisticsQueueClient;
    private readonly TableClient _statisticsTableClient;
    private const int MaxMessages = 32; // Azure Storage Queue limit

    public StatisticsProcessorFunction(
        ILogger<StatisticsProcessorFunction> logger,
        QueueClient statisticsQueueClient,
        TableServiceClient tableServiceClient)
    {
        _logger = logger;
        _statisticsQueueClient = statisticsQueueClient;
        _statisticsTableClient = tableServiceClient.GetTableClient("Statistics");
        _statisticsTableClient.CreateIfNotExists();
    }

    [Function("StatisticsProcessor")]
    public async Task Run(
        [TimerTrigger("0 */15 * * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Statistics processor starting at {Time}", DateTimeOffset.UtcNow);

        try
        {
            int totalProcessed = 0;
            QueueMessage[] messages;

            // Process messages in batches until queue is empty
            do
            {
                // Receive batch of messages (up to 32)
                var response = await _statisticsQueueClient.ReceiveMessagesAsync(
                    maxMessages: MaxMessages,
                    visibilityTimeout: TimeSpan.FromMinutes(5),
                    cancellationToken: cancellationToken);

                messages = response.Value;

                if (messages.Length == 0)
                {
                    _logger.LogInformation("No statistics messages to process");
                    break;
                }

                _logger.LogInformation("Processing {Count} statistics messages", messages.Length);

                // Aggregate in memory
                var aggregator = new StatisticsAggregator(_logger);

                foreach (var message in messages)
                {
                    try
                    {
                        var evt = JsonSerializer.Deserialize<StatisticsEvent>(
                            message.MessageText,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (evt != null)
                        {
                            aggregator.Add(evt);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to deserialize message: {MessageId}", message.MessageId);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Invalid JSON in message {MessageId}: {Text}", 
                            message.MessageId, message.MessageText);
                    }
                }

                // Batch upsert to Table Storage
                await aggregator.FlushToStorageAsync(_statisticsTableClient, cancellationToken);

                // Delete processed messages
                foreach (var message in messages)
                {
                    try
                    {
                        await _statisticsQueueClient.DeleteMessageAsync(
                            message.MessageId,
                            message.PopReceipt,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete message {MessageId}", message.MessageId);
                    }
                }

                totalProcessed += messages.Length;

                // Continue processing if we got a full batch (likely more messages in queue)
            } while (messages.Length == MaxMessages && !cancellationToken.IsCancellationRequested);

            _logger.LogInformation("Statistics processor completed. Processed {Count} messages", totalProcessed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Statistics processor failed");
            throw;
        }
    }
}
