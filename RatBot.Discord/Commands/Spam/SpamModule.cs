using RatBot.Application.Moderation;

namespace RatBot.Discord.Commands.Spam;

[Group("spam", "Spam moderation commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class SpamModule(ImageSpamSettingsService settingsService) : SlashCommandBase
{
    [SlashCommand("image-spam-config", "View or update image spam detection settings.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ImageSpamConfigAsync(
        [Summary("number-of-channels", "Distinct channels required within the burst duration.")]
        int? numberOfChannels = null,
        [Summary("number-of-required-attached-messages", "Messages with attachments required within the burst duration.")]
        int? numberOfRequiredAttachedMessages = null,
        [Summary("burst-duration", "Detection burst duration in seconds.")]
        int? burstDuration = null)
    {
        ErrorOr<Success> validationResult = Validate(
            numberOfChannels,
            numberOfRequiredAttachedMessages,
            burstDuration);

        if (validationResult.IsError)
        {
            await RespondAsync(validationResult.FirstError.Description, ephemeral: true).ConfigureAwait(false);
            return;
        }

        ImageBurstSpamDetectorOptions settings =
            numberOfChannels is null && numberOfRequiredAttachedMessages is null && burstDuration is null
                ? await settingsService.GetCurrentAsync(CancellationToken.None).ConfigureAwait(false)
                : await settingsService
                    .UpsertAsync(numberOfChannels, numberOfRequiredAttachedMessages, burstDuration, CancellationToken.None)
                    .ConfigureAwait(false);

        await RespondAsync(FormatSettings(settings), ephemeral: true).ConfigureAwait(false);
    }

    private static ErrorOr<Success> Validate(
        int? numberOfChannels,
        int? numberOfRequiredAttachedMessages,
        int? burstDuration)
    {
        if (numberOfChannels is <= 0)
            return Error.Validation(description: "Number of channels must be greater than zero.");

        if (numberOfRequiredAttachedMessages is <= 0)
            return Error.Validation(description: "Number of required attached messages must be greater than zero.");

        return burstDuration is <= 0
            ? Error.Validation(description: "Burst duration must be greater than zero seconds.")
            : Result.Success;
    }

    private static string FormatSettings(ImageBurstSpamDetectorOptions settings) =>
        "Image spam detection settings: "
        + $"{settings.DistinctChannelThreshold} channel(s), "
        + $"{settings.RequiredAttachedMessageCount} required attached message(s), "
        + $"{settings.Window}s burst duration.";
}
