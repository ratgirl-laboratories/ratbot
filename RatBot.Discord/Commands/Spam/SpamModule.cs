using RatBot.Application.Moderation;

namespace RatBot.Discord.Commands.Spam;

[Group("spam", "Spam moderation commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class SpamModule(ImageSpamSettingsService settingsService) : SlashCommandBase
{

    private static ErrorOr<Success> Validate(
        int? numberOfChannels,
        int? attachmentCount,
        int? burstDuration)
    {
        if (numberOfChannels is <= 0)
            return Error.Validation(description: "Number of channels must be greater than zero.");

        if (attachmentCount is <= 0)
            return Error.Validation(description: "Attachment count must be greater than zero.");

        return burstDuration is <= 0
            ? Error.Validation(description: "Burst duration must be greater than zero seconds.")
            : Result.Success;
    }

    private static string FormatSettings(ImageBurstSpamDetectorOptions settings) =>
        "Image spam detection settings: "
        + $"{settings.DistinctChannelThreshold} channel(s), "
        + $"{settings.RequiredAttachmentCount} attachment(s) per message, "
        + $"{settings.Window}s burst duration.";
    [SlashCommand("image-spam-config", "View or update image spam detection settings.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ImageSpamConfigAsync(
        [Summary("number-of-channels", "Distinct channels required within the burst duration.")]
        int? numberOfChannels = null,
        [Summary("attachment-count", "Attachments required on a message before it counts toward detection.")]
        int? attachmentCount = null,
        [Summary("burst-duration", "Detection burst duration in seconds.")]
        int? burstDuration = null)
    {
        ErrorOr<Success> validationResult = Validate(
            numberOfChannels,
            attachmentCount,
            burstDuration);

        if (validationResult.IsError)
        {
            await RespondAsync(validationResult.FirstError.Description, ephemeral: true).ConfigureAwait(false);
            return;
        }

        ImageBurstSpamDetectorOptions settings =
            numberOfChannels is null && attachmentCount is null && burstDuration is null
                ? await settingsService.GetCurrentAsync(CancellationToken.None).ConfigureAwait(false)
                : await settingsService
                    .UpsertAsync(numberOfChannels, attachmentCount, burstDuration, CancellationToken.None)
                    .ConfigureAwait(false);

        await RespondAsync(FormatSettings(settings), ephemeral: true).ConfigureAwait(false);
    }
}