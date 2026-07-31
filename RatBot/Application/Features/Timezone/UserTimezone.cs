namespace RatBot.Application.Features.Timezone;

public sealed record UserTimezone(ulong UserId, IanaTimezoneId Timezone, DateTimeOffset UpdatedAtUtc);
