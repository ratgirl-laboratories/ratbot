using Microsoft.Extensions.DependencyInjection;
using RatBot.Application.Features.Moderation.Interfaces;
using RatBot.Domain.Features.Moderation;

namespace RatBot.Host.Discord;

public sealed class AutobanGatewayHandler(
    DiscordSocketClient discordClient,
    IServiceScopeFactory scopeFactory,
    ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<AutobanGatewayHandler>();

    public void Subscribe()
    {
        discordClient.UserJoined += HandleUserJoinedAsync;
    }

    public void Unsubscribe()
    {
        discordClient.UserJoined -= HandleUserJoinedAsync;
    }

    private async Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        ulong guildId = user.Guild.Id;
        ulong userId = user.Id;

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IModerationService moderationService = scope.ServiceProvider.GetRequiredService<IModerationService>();

            AutobannedUser? autobannedUser = await moderationService.GetAutobanAsync(guildId, userId);

            if (autobannedUser is null)
                return;

            string reason =
                $"Autoban registered by moderator {autobannedUser.Moderator} at {autobannedUser.RegisteredAtUtc:O}.";

            await user.Guild.AddBanAsync(user.Id, 0, reason);

            _logger.Information(
                "Banned autobanned user {UserId} immediately after joining guild {GuildId}.",
                userId,
                guildId);
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "Failed processing autoban join check for user {UserId} in guild {GuildId}.",
                userId,
                guildId);
        }
    }
}
