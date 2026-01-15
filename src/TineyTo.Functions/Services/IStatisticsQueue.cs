using TineyTo.Functions.Models;

namespace TineyTo.Functions.Services;

/// <summary>
/// Interface for queuing statistics events for batch processing.
/// </summary>
public interface IStatisticsQueue
{
    /// <summary>
    /// Queue a statistics event for processing.
    /// </summary>
    /// <param name="event">The statistics event to queue</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task that completes when the event is queued</returns>
    Task QueueEventAsync(StatisticsEvent @event, CancellationToken cancellationToken = default);
}
