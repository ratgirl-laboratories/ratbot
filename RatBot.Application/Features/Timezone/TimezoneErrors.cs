namespace RatBot.Application.Features.Timezone;

public static class TimeZoneErrors
{
    public static Error InvalidInput =>
        Error.Validation(
            code: "TimeZone.InvalidInput",
            description: "Use an IANA timezone ID such as Europe/London, America/New_York, Australia/Sydney, etc."
        );
}
