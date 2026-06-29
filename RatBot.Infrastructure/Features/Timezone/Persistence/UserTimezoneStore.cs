using Dapper;
using Npgsql;
using RatBot.Application.Features.Timezone;

namespace RatBot.Infrastructure.Features.Timezone.Persistence;

public sealed class UserTimezoneStore(string connectionString) : IUserTimezoneStore
{
    private const string Columns = """
        user_id AS "UserId",
        timezone_id AS "TimezoneId",
        updated_at_utc AS "UpdatedAtUtc"
        """;

    public async Task<ErrorOr<UserTimezone>> GetAsync(ulong userId, CancellationToken ct = default)
    {
        await using NpgsqlConnection connection = CreateConnection();
        await connection.OpenAsync(ct).ConfigureAwait(false);

        Data? data = await connection
            .QuerySingleOrDefaultAsync<Data>(
                new CommandDefinition(
                    $"""
                    SELECT {Columns}
                    FROM public.user_timezones
                    WHERE user_id = @UserId
                    """,
                    new { UserId = ToDatabaseId(userId) },
                    cancellationToken: ct
                )
            )
            .ConfigureAwait(false);

        return data is null ? TimezoneErrors.NotConfigured : ToDomain(data);
    }

    public async Task<ErrorOr<UserTimezone>> SetAsync(
        ulong userId,
        IanaTimezoneId timezone,
        DateTimeOffset updatedAtUtc,
        CancellationToken ct = default
    )
    {
        await using NpgsqlConnection connection = CreateConnection();
        await connection.OpenAsync(ct).ConfigureAwait(false);

        Data data = await connection
            .QuerySingleAsync<Data>(
                new CommandDefinition(
                    $"""
                    INSERT INTO public.user_timezones (user_id, timezone_id, updated_at_utc)
                    VALUES (@UserId, @TimezoneId, @UpdatedAtUtc)
                    ON CONFLICT (user_id) DO UPDATE
                    SET timezone_id = EXCLUDED.timezone_id,
                        updated_at_utc = EXCLUDED.updated_at_utc
                    RETURNING {Columns}
                    """,
                    new
                    {
                        UserId = ToDatabaseId(userId),
                        TimezoneId = timezone.Value,
                        UpdatedAtUtc = updatedAtUtc,
                    },
                    cancellationToken: ct
                )
            )
            .ConfigureAwait(false);

        return ToDomain(data);
    }

    private static UserTimezone ToDomain(Data data)
    {
        ErrorOr<IanaTimezoneId> timezone = IanaTimezoneId.Create(data.TimezoneId);

        if (timezone.IsError)
            throw new InvalidOperationException("Persisted user timezone is invalid.");

        return new UserTimezone(ToDomainId(data.UserId), timezone.Value, data.UpdatedAtUtc);
    }

    private static long ToDatabaseId(ulong id) => checked((long)id);

    private static ulong ToDomainId(long id) => checked((ulong)id);

    private NpgsqlConnection CreateConnection() => new NpgsqlConnection(connectionString);

    private sealed class Data
    {
        public long UserId { get; set; }
        public string TimezoneId { get; set; } = null!;
        public DateTimeOffset UpdatedAtUtc { get; set; }
    }
}
