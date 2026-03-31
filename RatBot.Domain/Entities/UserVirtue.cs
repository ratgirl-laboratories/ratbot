using System.ComponentModel.DataAnnotations.Schema;

namespace RatBot.Domain.Entities;

[Table("UserScores")]
public sealed class UserVirtue
{
    public required ulong UserId { get; init; }
    public required int Virtue { get; set; }
}
