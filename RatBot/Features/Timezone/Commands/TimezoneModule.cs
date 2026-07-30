using RatBot.Application.Features.Timezone;

namespace RatBot.Features.Timezone.Commands;

public sealed class TimezoneModule(UserTimezoneOperations operations) : InteractionModuleBase<IInteractionContext>
{
    [SlashCommand("timezone", "Set your private timezone.")]
    public async Task SetAsync([Summary("timezone", "An IANA timezone ID.")] string timezone)
    {
        ErrorOr<UserTimezone> result = await operations.SetAsync(
            new SetUserTimezoneCommand(Context.User.Id, timezone, DateTimeOffset.UtcNow),
            CancellationToken.None
        );

        await result.SwitchFirstAsync(
            async userTimezone => await RespondAsync(SuccessResponse(userTimezone), ephemeral: true),
            async error => await RespondAsync(error.Description, ephemeral: true)
        );
    }

    private static string SuccessResponse(UserTimezone userTimezone)
    {
        TimeZoneInfo timezone = userTimezone.Timezone.Resolve();
        DateTimeOffset localTime = TimeZoneInfo.ConvertTime(userTimezone.UpdatedAtUtc, timezone);

        return $"Timezone set to {userTimezone.Timezone.Value}. Current local time: {localTime:yyyy-MM-dd HH:mm zzz}.";
    }
}
