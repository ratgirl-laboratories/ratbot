using ErrorOr;
using RatBot.Application.Features.Timezone;
using Shouldly;

namespace RatBot.Application.Tests.Features.Timezone;

public sealed class UserTimezoneOperationsTests
{
    private const ulong UserId = 123;
    private static readonly DateTimeOffset NowUtc = new DateTimeOffset(2026, 6, 29, 12, 30, 0, TimeSpan.Zero);

    [Test]
    public async Task SetAsync_rejects_invalid_timezone_input_and_does_not_call_store()
    {
        FakeUserTimezoneStore store = new FakeUserTimezoneStore();
        UserTimezoneOperations operations = new UserTimezoneOperations(store);

        ErrorOr<UserTimezone> result = await operations.SetAsync(new SetUserTimezoneCommand(UserId, "BST", NowUtc));

        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldBe(TimezoneErrors.InvalidInput.Code);
        store.SetCallCount.ShouldBe(0);
    }

    [Test]
    public async Task SetAsync_stores_valid_timezone_input()
    {
        FakeUserTimezoneStore store = new FakeUserTimezoneStore();
        UserTimezoneOperations operations = new UserTimezoneOperations(store);

        ErrorOr<UserTimezone> result = await operations.SetAsync(new SetUserTimezoneCommand(UserId, "europe/london", NowUtc));

        result.IsError.ShouldBeFalse();
        result.Value.UserId.ShouldBe(UserId);
        result.Value.Timezone.Value.ShouldBe("Europe/London");
        result.Value.UpdatedAtUtc.ShouldBe(NowUtc);
        store.SetCallCount.ShouldBe(1);
        store.LastSetUserId.ShouldBe(UserId);
        store.LastSetTimezone!.Value.ShouldBe("Europe/London");
        store.LastSetUpdatedAtUtc.ShouldBe(NowUtc);
    }

    [Test]
    public async Task GetAsync_delegates_to_store_and_returns_existing_timezone()
    {
        UserTimezone existing = CreateUserTimezone("UTC");
        FakeUserTimezoneStore store = new FakeUserTimezoneStore { GetResult = existing };
        UserTimezoneOperations operations = new UserTimezoneOperations(store);

        ErrorOr<UserTimezone> result = await operations.GetAsync(UserId);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(existing);
        store.GetCallCount.ShouldBe(1);
        store.LastGetUserId.ShouldBe(UserId);
    }

    [Test]
    public async Task RequireAsync_returns_existing_timezone()
    {
        UserTimezone existing = CreateUserTimezone("Europe/London");
        FakeUserTimezoneStore store = new FakeUserTimezoneStore { GetResult = existing };
        UserTimezoneOperations operations = new UserTimezoneOperations(store);

        ErrorOr<UserTimezone> result = await operations.RequireAsync(UserId);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(existing);
    }

    [Test]
    public async Task RequireAsync_returns_not_configured_when_user_has_no_timezone()
    {
        FakeUserTimezoneStore store = new FakeUserTimezoneStore { GetResult = TimezoneErrors.NotConfigured };
        UserTimezoneOperations operations = new UserTimezoneOperations(store);

        ErrorOr<UserTimezone> result = await operations.RequireAsync(UserId);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(TimezoneErrors.NotConfigured);
    }

    [Test]
    public async Task RequireAsync_preserves_unrelated_store_errors()
    {
        Error storeError = Error.Failure("TimeZone.StoreFailed", "Store failed.");
        FakeUserTimezoneStore store = new FakeUserTimezoneStore { GetResult = storeError };
        UserTimezoneOperations operations = new UserTimezoneOperations(store);

        ErrorOr<UserTimezone> result = await operations.RequireAsync(UserId);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(storeError);
    }

    private static UserTimezone CreateUserTimezone(string timezoneId) => new UserTimezone(UserId, IanaTimezoneId.Create(timezoneId).Value, NowUtc);

    private sealed class FakeUserTimezoneStore : IUserTimezoneStore
    {
        public ErrorOr<UserTimezone>? GetResult { get; set; }

        public int GetCallCount { get; private set; }

        public ulong LastGetUserId { get; private set; }

        public int SetCallCount { get; private set; }

        public ulong LastSetUserId { get; private set; }

        public IanaTimezoneId? LastSetTimezone { get; private set; }

        public DateTimeOffset LastSetUpdatedAtUtc { get; private set; }

        public Task<ErrorOr<UserTimezone>> GetAsync(ulong userId, CancellationToken ct = default)
        {
            GetCallCount++;
            LastGetUserId = userId;

            return Task.FromResult(GetResult ?? TimezoneErrors.NotConfigured);
        }

        public Task<ErrorOr<UserTimezone>> SetAsync(
            ulong userId,
            IanaTimezoneId timezone,
            DateTimeOffset updatedAtUtc,
            CancellationToken ct = default
        )
        {
            SetCallCount++;
            LastSetUserId = userId;
            LastSetTimezone = timezone;
            LastSetUpdatedAtUtc = updatedAtUtc;

            return Task.FromResult<ErrorOr<UserTimezone>>(new UserTimezone(userId, timezone, updatedAtUtc));
        }
    }
}
