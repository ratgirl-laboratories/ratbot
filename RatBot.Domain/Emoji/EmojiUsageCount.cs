namespace RatBot.Domain.Emoji;

/// <summary>
///     Represents persisted usage metrics for an emoji.
/// </summary>
public sealed class EmojiUsageCount
{
    /// <summary>
    ///     Gets or sets the emoji identifier.
    /// </summary>
    public required string EmojiId { get; set; }

    /// <summary>
    ///     Gets or sets the cumulative usage count.
    /// </summary>
    public required int UsageCount { get; set; }
}