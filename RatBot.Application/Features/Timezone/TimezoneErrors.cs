namespace RatBot.Application.Features.Timezone;

public static class TimezoneErrors
{
    public static Error InvalidInput =>
        Error.Validation(
            code: "TimeZone.InvalidInput",
            description: "Use an IANA timezone ID such as UTC, Europe/London, America/New_York, or Australia/Sydney."
        );

    public static Error NotConfigured => Error.Validation(code: "TimeZone.NotConfigured", description: "You do not have a timezone configured.");
}
