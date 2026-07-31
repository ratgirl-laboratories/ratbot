namespace RatBot.Application.Features.Timezone;

/// <summary>
/// Provides persistence operations for user-configured timezones.
/// </summary>
public interface IUserTimezoneStore
{
    /// <summary>
    /// Gets the configured timezone for a Discord user.
    /// </summary>
    /// <param name="userId">The Discord user ID whose timezone should be retrieved.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///     The configured timezone for the user, or an application error if no timezone has been configured.
    /// </returns>
    Task<ErrorOr<UserTimezone>> GetAsync(ulong userId, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates the configured timezone for a Discord user.
    /// </summary>
    /// <param name="userId">The Discord user ID whose timezone should be stored.</param>
    /// <param name="timezone">The validated IANA timezone ID to store for the user.</param>
    /// <param name="updatedAtUtc">The UTC time when the timezone was updated.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored timezone record.</returns>
    Task<ErrorOr<UserTimezone>> SetAsync(ulong userId, IanaTimezoneId timezone, DateTimeOffset updatedAtUtc, CancellationToken ct = default);
}
