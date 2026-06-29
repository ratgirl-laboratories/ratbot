namespace RatBot.Application.Features.Timezone;

/// <summary>
/// Provides application workflows for user-configured timezones.
/// </summary>
public sealed class UserTimezoneOperations(IUserTimezoneStore store)
{
    /// <summary>
    /// Stores a user's configured timezone.
    /// </summary>
    /// <param name="command">The request to set the user's timezone.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored timezone record, or a validation error if the timezone is invalid.</returns>
    public async Task<ErrorOr<UserTimezone>> SetAsync(SetUserTimezoneCommand command, CancellationToken ct = default)
    {
        ErrorOr<IanaTimezoneId> timezoneResult = IanaTimezoneId.Create(command.RawTimezoneInput);

        if (timezoneResult.IsError)
            return timezoneResult.Errors;

        return await store.SetAsync(command.UserId, timezoneResult.Value, command.NowUtc, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the configured timezone for a Discord user.
    /// </summary>
    /// <param name="userId">The Discord user ID whose timezone should be retrieved.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The configured timezone for the user, or an application error if no timezone
    /// has been configured.
    /// </returns>
    public Task<ErrorOr<UserTimezone>> GetAsync(ulong userId, CancellationToken ct = default) => store.GetAsync(userId, ct);

    /// <summary>
    /// Requires that a Discord user has configured a timezone.
    /// </summary>
    /// <param name="userId">The Discord user ID to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The configured timezone for the user, or an application error explaining that
    /// a timezone must be configured first.
    /// </returns>
    public async Task<ErrorOr<UserTimezone>> RequireAsync(ulong userId, CancellationToken ct = default)
    {
        ErrorOr<UserTimezone> timezoneResult = await store.GetAsync(userId, ct).ConfigureAwait(false);

        if (!timezoneResult.IsError)
            return timezoneResult.Value;

        return timezoneResult.Errors.Any(error => string.Equals(error.Code, TimezoneErrors.NotConfigured.Code, StringComparison.Ordinal))
            ? TimezoneErrors.NotConfigured
            : timezoneResult.Errors;
    }
}
