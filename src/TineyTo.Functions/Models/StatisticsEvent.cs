using System.Text.Json.Serialization;

namespace TineyTo.Functions.Models;

/// <summary>
/// Represents a statistics event to be queued for batch processing.
/// </summary>
public class StatisticsEvent
{
    /// <summary>
    /// Type of event
    /// </summary>
    [JsonPropertyName("eventType")]
    public required StatisticsEventType EventType { get; init; }

    /// <summary>
    /// UTC timestamp when the event occurred
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// The short URL alias involved in this event
    /// </summary>
    [JsonPropertyName("alias")]
    public required string Alias { get; init; }
}
