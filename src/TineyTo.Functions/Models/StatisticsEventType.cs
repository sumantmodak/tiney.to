using System.Text.Json.Serialization;

namespace TineyTo.Functions.Models;

/// <summary>
/// Type of statistics event for efficient comparison.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StatisticsEventType
{
    /// <summary>
    /// A new short link was created
    /// </summary>
    LinkCreated = 0,
    
    /// <summary>
    /// A short link was accessed (redirect occurred)
    /// </summary>
    Redirect = 1
}
