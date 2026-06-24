namespace RatBot.Domain.Emoji;

/// <summary>
///     Represents persisted usage metrics for an emoji.
/// </summary>
public sealed class EmojiUsageCount
{
    /// <summary>
    ///     The snowflake ID for the emoji
    /// </summary>
    public required ulong EmojiId { get; set; }

    /// <summary>
    ///     The number of times the emoji has been used as a reaction
    /// </summary>
    public required int ReactionUsageCount { get; set; }

    /// <summary>
    ///     The number of times the emoji has been used in a message
    /// </summary>
    public required int MessageUsageCount { get; set; }
}
