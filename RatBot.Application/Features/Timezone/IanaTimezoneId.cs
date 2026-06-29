namespace RatBot.Application.Features.Timezone;

/// <summary>
/// Represents a validated IANA timezone identifier.
/// </summary>
public sealed record IanaTimezoneId
{
    private IanaTimezoneId(string value)
    {
        Value = value;
    }

    /// <summary>
    /// The canonical IANA timezone identifier.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a validated IANA timezone identifier from user input.
    /// </summary>
    public static ErrorOr<IanaTimezoneId> Create(string rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
            return TimezoneErrors.InvalidInput;

        string candidate = rawInput.Trim();

        if (!LooksLikeIanaTimeZoneId(candidate))
            return TimezoneErrors.InvalidInput;

        TimeZoneInfo? timeZone = TimeZoneInfo
            .GetSystemTimeZones()
            .FirstOrDefault(timeZone => string.Equals(timeZone.Id, candidate, StringComparison.OrdinalIgnoreCase));

        return timeZone is null ? TimezoneErrors.InvalidInput : new IanaTimezoneId(timeZone.Id);
    }

    private static bool LooksLikeIanaTimeZoneId(string value) =>
        string.Equals(value, "UTC", StringComparison.OrdinalIgnoreCase) || value.Contains('/', StringComparison.Ordinal);

    /// <summary>
    /// Resolves this timezone identifier to the runtime timezone information.
    /// </summary>
    public TimeZoneInfo Resolve() => TimeZoneInfo.FindSystemTimeZoneById(Value);

    /// <inheritdoc />
    public override string ToString() => Value;
}
