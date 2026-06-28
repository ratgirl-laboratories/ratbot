using Discord.Net;
using RatBot.Discord.Gateway;
using RatBot.Domain.SecretRole;

namespace RatBot.Discord.SecretRole;

public sealed class SecretRoleGatewayHandler(DiscordSocketClient discordClient, SecretRoleManager manager, ILogger logger) : IDiscordGatewayHandler
{
    private readonly ILogger _logger = logger.ForContext<SecretRoleGatewayHandler>();

    public async Task InitializeAsync(CancellationToken ct)
    {
        await manager.InitializeAsync(ct).ConfigureAwait(false);
        Subscribe();
    }

    public void Unsubscribe() => discordClient.MessageReceived -= HandleMessageReceivedAsync;

    private async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        if (
            message is not SocketUserMessage { Source: MessageSource.User } userMessage
            || userMessage.Author.IsBot
            || userMessage.Author.IsWebhook
            || userMessage.Author is not SocketGuildUser guildUser
            || userMessage.Channel is not SocketGuildChannel guildChannel
        )
            return;

        SecretRoleSetting? setting = manager.Current;

        if (
            setting is null
            || setting.GuildId != guildChannel.Guild.Id
            || !userMessage.MentionedRoleIds.Contains(setting.RoleId)
            || guildUser.Roles.Any(role => role.Id == setting.RoleId)
        )
            return;

        SocketRole? role = guildChannel.Guild.GetRole(setting.RoleId);

        if (role is null)
        {
            _logger.Warning("Configured temporary ping role {RoleId} no longer exists in guild {GuildId}.", setting.RoleId, setting.GuildId);
            return;
        }

        try
        {
            await guildUser
                .AddRoleAsync(
                    role,
                    new RequestOptions { AuditLogReason = $"Self-awarded by mentioning the configured secret role in message {message.Id}." }
                )
                .ConfigureAwait(false);

            _logger.Information(
                "Granted temporary ping role {RoleId} to user {UserId} in guild {GuildId}.",
                role.Id,
                guildUser.Id,
                guildUser.Guild.Id
            );
        }
        catch (HttpException ex)
        {
            _logger.Error(
                ex,
                "Discord rejected granting temporary ping role {RoleId} to user {UserId} in guild {GuildId}.",
                role.Id,
                guildUser.Id,
                guildUser.Guild.Id
            );
        }
    }

    private void Subscribe() => discordClient.MessageReceived += HandleMessageReceivedAsync;
}
