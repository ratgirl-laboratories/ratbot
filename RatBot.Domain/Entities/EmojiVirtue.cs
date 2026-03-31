using System.ComponentModel.DataAnnotations.Schema;

namespace RatBot.Domain.Entities;

[Table("ReactionEmojiScores")]
public sealed class EmojiVirtue
{
    public required string EmojiId { get; set; }
    public required int Virtue { get; set; }
}
