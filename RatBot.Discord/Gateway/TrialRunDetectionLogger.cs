namespace RatBot.Discord.Gateway;

public sealed class TrialRunDetectionLogger(DiscordSocketClient discordClient, ILogger logger)
{
    private const ulong ChannelId = 1162879028223561849;
    private const ulong ModeratorRoleId = 268886789983436800;

    private readonly ILogger _logger = logger.ForContext<TrialRunDetectionLogger>();

    public async Task LogImageSpamBanAsync(ulong bannedUserId)
    {
        try
        {
            if (discordClient.GetChannel(ChannelId) is not IMessageChannel channel)
            {
                _logger.Warning(
                    "Cannot send trial-run image spam detection message because channel {ChannelId} was not found.",
                    ChannelId);
                return;
            }

            string message =
                $"<@&{ModeratorRoleId}> - likely spambot identified and banned. See tickets for <@{bannedUserId}> ({bannedUserId}).";

            AllowedMentions allowedMentions = new AllowedMentions
            {
                RoleIds = new List<ulong> { ModeratorRoleId },
                UserIds = new List<ulong> { bannedUserId },
            };

            await channel.SendMessageAsync(message, allowedMentions: allowedMentions).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Warning(
                ex,
                "Failed to send trial-run image spam detection message for banned user {UserId}.",
                bannedUserId);
        }
    }
}
