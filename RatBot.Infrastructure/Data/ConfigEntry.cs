namespace RatBot.Infrastructure.Data;

/// <summary>
/// Represents a persisted bot configuration entry.
/// </summary>
public sealed class ConfigEntry
{
    /// <summary>
    /// Gets or sets the stable config key.
    /// </summary>
    public required string Key { get; set; }

    /// <summary>
    /// Gets or sets the serialized config payload.
    /// </summary>
    public required string Value { get; set; }
}
