using Microsoft.Extensions.Options;
using RatBot.Application.Moderation;
using RatBot.Discord.Configuration;

namespace RatBot.Discord.Gateway;

public sealed class ImageBurstSpamGatewayHandler(
    DiscordSocketClient discordClient,
    ImageBurstSpamDetector detector,
    IOptions<DiscordOptions> options,
    ILogger logger)
    : IDiscordGatewayHandler
{
    private const int MinimumAttachmentCount = 2;

    private readonly ILogger _logger = logger.ForContext<ImageBurstSpamGatewayHandler>();
    private readonly DiscordOptions _options = options.Value;

    public Task InitializeAsync(CancellationToken ct)
    {
        Subscribe();
        return Task.CompletedTask;
    }

    public void Unsubscribe() => discordClient.MessageReceived -= HandleMessageReceivedAsync;

    private void Subscribe() => discordClient.MessageReceived += HandleMessageReceivedAsync;

    private async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        try
        {
            ImageBurstMessage? qualifyingMessage = TryCreateQualifyingMessage(message);

            if (qualifyingMessage is null)
                return;

            ImageBurstDetection? detection = detector.Observe(qualifyingMessage);

            if (detection is null)
                return;

            await HandleDetectionAsync(detection).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed processing image-burst spam detection for message {MessageId}.", message.Id);
        }
    }

    private ImageBurstMessage? TryCreateQualifyingMessage(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage)
            return null;

        if (userMessage.Source != MessageSource.User)
            return null;

        if (userMessage.Author.IsBot || userMessage.Author.IsWebhook)
            return null;

        if (userMessage.Channel is not SocketTextChannel channel)
            return null;

        if (userMessage.Author is not SocketGuildUser guildUser)
            return null;

        if (userMessage.Attachments.Count < MinimumAttachmentCount)
            return null;

        if (IsExempt(guildUser))
            return null;

        ImageBurstAttachment[] attachments = userMessage.Attachments
            .Select(x => new ImageBurstAttachment(x.Url))
            .ToArray();

        return new ImageBurstMessage(
            channel.Guild.Id,
            guildUser.Id,
            channel.Id,
            userMessage.Timestamp,
            attachments);
    }

    private bool IsExempt(SocketGuildUser user) =>
        _options.ImageBurstSpamAllowlistedUserIds.Contains(user.Id)
        || user.Roles.Any(role => _options.ImageBurstSpamAllowlistedRoleIds.Contains(role.Id))
        || HasStaffPermissions(user.GuildPermissions);

    private static bool HasStaffPermissions(GuildPermissions permissions) =>
        permissions.Administrator
        || permissions.ManageGuild
        || permissions.ManageMessages
        || permissions.BanMembers
        || permissions.KickMembers
        || permissions.ModerateMembers;

    private async Task HandleDetectionAsync(ImageBurstDetection detection)
    {
        SocketGuild guild = discordClient.GetGuild(detection.GuildId);

        string reason = $"Automatic image-burst spam detection: {detection.ChannelIds.Count} channels in 45 seconds.";

        await guild
            .AddBanAsync(detection.UserId, _options.ImageBurstSpamHistoryPruneDays, reason)
            .ConfigureAwait(false);

        _logger.Warning(
            "Banned user {UserId} for image-burst spam across {ChannelCount} channels.",
            detection.UserId,
            detection.ChannelIds.Count);
    }
}
