using System.ComponentModel.DataAnnotations.Schema;

namespace RatBot.Domain.Entities;

[Table("ReactionEmojiScores")]
public sealed class ReactionEmojiScore
{
    public required string EmojiId { get; set; }
    public required int Score { get; set; }
}
