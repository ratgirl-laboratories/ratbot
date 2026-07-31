namespace RatBot.Application.Features.Timezone;

public readonly record struct SetUserTimezoneCommand(ulong UserId, string RawTimezoneInput, DateTimeOffset NowUtc);
