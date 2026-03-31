using System.ComponentModel.DataAnnotations.Schema;

namespace RatBot.Domain.Entities;

[Table("UserScores")]
public sealed class UserScore
{
    public required ulong UserId { get; init; }
    public required int Score { get; set; }
}
