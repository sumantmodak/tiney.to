namespace TineyTo.Functions.Configuration;

/// <summary>
/// Configuration for statistics queue.
/// </summary>
public class StatisticsConfiguration
{
    public required string QueueConnection { get; init; }
    public required string QueueName { get; init; }

    public static StatisticsConfiguration LoadFromEnvironment()
    {
        return new StatisticsConfiguration
        {
            QueueConnection = Environment.GetEnvironmentVariable("STATISTICS_QUEUE_CONNECTION") 
                ?? throw new InvalidOperationException("STATISTICS_QUEUE_CONNECTION not configured"),
            QueueName = Environment.GetEnvironmentVariable("STATISTICS_QUEUE_NAME") 
                ?? "statistics-events"
        };
    }
}
