namespace RatBot.Infrastructure.Features.Timezone.Persistence;

public sealed class UserTimezoneRow
{
    public long UserId { get; set; }
    public string TimezoneId { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
