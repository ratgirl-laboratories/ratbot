using RatBot.Discord.BackgroundWorkers;

namespace RatBot.Discord.Gateway;

public sealed class UserUpdatedGatewayHandler(DiscordSocketClient discordClient, IRoleColourSyncQueue syncQueue, ILogger logger)
    : IDiscordGatewayHandler
{
    private readonly ILogger _logger = logger.ForContext<UserUpdatedGatewayHandler>();

    public Task InitializeAsync(CancellationToken ct)
    {
        Subscribe();
        return Task.CompletedTask;
    }

    public void Unsubscribe() => discordClient.GuildMemberUpdated -= HandleGuildMemberUpdated;

    private void Subscribe() => discordClient.GuildMemberUpdated += HandleGuildMemberUpdated;

    private Task HandleGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> before, SocketGuildUser after)
    {
        try
        {
            try
            {
                // We only care about role changes; if we can't get the before snapshot, conservatively reconcile.
                IReadOnlyCollection<ulong>? beforeRoles = null;

                if (before.HasValue)
                    beforeRoles = ((IGuildUser)before.Value).RoleIds;

                IReadOnlyCollection<ulong> afterRoles = ((IGuildUser)after).RoleIds;

                bool rolesChanged =
                    beforeRoles is null
                    || beforeRoles.Count != afterRoles.Count
                    || !beforeRoles.OrderBy(x => x).SequenceEqual(afterRoles.OrderBy(x => x));

                if (!rolesChanged)
                    return Task.CompletedTask;

                _logger
                    .ForContext("GuildId", after.Guild.Id)
                    .ForContext("UserId", after.Id)
                    .ForContext("BeforeRoleCount", beforeRoles?.Count)
                    .ForContext("AfterRoleCount", afterRoles.Count)
                    .Debug("Guild member roles changed; enqueued role-colour reconciliation.");

                syncQueue.Enqueue(after.Guild.Id, after.Id);
            }
            catch (Exception ex)
            {
                _logger
                    .ForContext("GuildId", after.Guild.Id)
                    .ForContext("UserId", after.Id)
                    .Error(ex, "Failed to reconcile after guild member update.");
            }

            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }
}
