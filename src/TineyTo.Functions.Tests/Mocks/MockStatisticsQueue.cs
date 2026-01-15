using TineyTo.Functions.Models;
using TineyTo.Functions.Services;

namespace TineyTo.Functions.Tests.Mocks;

/// <summary>
/// Mock implementation of IStatisticsQueue for testing.
/// Records events but doesn't actually queue them.
/// </summary>
public class MockStatisticsQueue : IStatisticsQueue
{
    public List<StatisticsEvent> QueuedEvents { get; } = new();

    public Task QueueEventAsync(StatisticsEvent @event, CancellationToken cancellationToken = default)
    {
        QueuedEvents.Add(@event);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        QueuedEvents.Clear();
    }
}
