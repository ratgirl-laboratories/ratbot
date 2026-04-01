using System.ComponentModel.DataAnnotations.Schema;

namespace RatBot.Domain.Entities;

[Table("VirtueReactionLocks")]
public sealed class VirtueReactionLock
{
    public required ulong MessageId { get; init; }
    public required ulong ReactorUserId { get; init; }
    public required ulong TargetUserId { get; init; }
    public required string EmojiId { get; init; }
    public required int VirtueDelta { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
