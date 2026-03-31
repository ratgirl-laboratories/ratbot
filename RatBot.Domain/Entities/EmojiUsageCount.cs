using System.ComponentModel.DataAnnotations.Schema;

namespace RatBot.Domain.Entities;

[Table("EmojiUsageCounts")]
public sealed class EmojiUsageCount
{
    public required string EmojiId { get; set; }
    public required int UsageCount { get; set; }
}
