using System.Collections.Immutable;
using RatBot.Discord.Gateway;

namespace RatBot.Discord.Features.Quorum;

public sealed class DiscordQuorumMemberIndex(DiscordSocketClient client, ILogger logger) : IDiscordGatewayHandler
{
    private readonly Lock _lock = new Lock();
    private readonly SemaphoreSlim _warmupLock = new SemaphoreSlim(1, 1);
    private readonly Dictionary<ulong, GuildIndex> _guilds = [];
    private readonly ILogger _logger = logger.ForContext<DiscordQuorumMemberIndex>();

    public Task InitializeAsync(CancellationToken ct)
    {
        client.UserJoined += HandleUserJoinedAsync;
        client.UserLeft += HandleUserLeftAsync;
        client.GuildMemberUpdated += HandleGuildMemberUpdatedAsync;
        client.RoleDeleted += HandleRoleDeletedAsync;

        return Task.CompletedTask;
    }

    public void Unsubscribe()
    {
        client.UserJoined -= HandleUserJoinedAsync;
        client.UserLeft -= HandleUserLeftAsync;
        client.GuildMemberUpdated -= HandleGuildMemberUpdatedAsync;
        client.RoleDeleted -= HandleRoleDeletedAsync;
    }

    public async Task EnsureTrackingAsync(SocketGuild guild, ImmutableHashSet<ulong> roleIds, CancellationToken ct)
    {
        ImmutableHashSet<ulong> missingRoleIds = GetMissingRoleIds(guild.Id, roleIds);

        if (missingRoleIds.IsEmpty)
            return;

        await _warmupLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            missingRoleIds = GetMissingRoleIds(guild.Id, roleIds);

            if (missingRoleIds.IsEmpty)
                return;

            PrepareRoleSetsForWarmup(guild.Id, missingRoleIds);

            _logger
                .ForContext("GuildId", guild.Id)
                .ForContext("RoleCount", missingRoleIds.Count)
                .Information("Warming quorum member index for configured voter roles.");

            if (guild.HasAllMembers)
            {
                foreach (SocketGuildUser user in guild.Users)
                    IndexUserForRoles(user, missingRoleIds);
            }
            else
            {
                RequestOptions requestOptions = new RequestOptions { CancelToken = ct };

                await foreach (IReadOnlyCollection<IGuildUser> page in guild.GetUsersAsync(requestOptions).WithCancellation(ct).ConfigureAwait(false))
                {
                    foreach (IGuildUser user in page)
                        IndexUserForRoles(user, missingRoleIds);
                }
            }

            MarkRolesReady(guild.Id, missingRoleIds);

            _logger
                .ForContext("GuildId", guild.Id)
                .ForContext("RoleCount", missingRoleIds.Count)
                .Information("Quorum member index warmed for configured voter roles.");
        }
        finally
        {
            _warmupLock.Release();
        }
    }

    public bool TryCountEligibleVoters(ulong guildId, ImmutableHashSet<ulong> roleIds, out int eligibleVoterCount)
    {
        lock (_lock)
        {
            eligibleVoterCount = 0;

            if (!_guilds.TryGetValue(guildId, out GuildIndex? guild))
                return false;

            if (!roleIds.All(guild.WarmedRoleIds.Contains))
                return false;

            HashSet<ulong> distinctUserIds = [];

            foreach (ulong roleId in roleIds)
            {
                if (guild.RoleMembers.TryGetValue(roleId, out HashSet<ulong>? userIds))
                    distinctUserIds.UnionWith(userIds);
            }

            eligibleVoterCount = distinctUserIds.Count;

            return true;
        }
    }

    private Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        try
        {
            IndexUserForRoles(user, GetTrackedRoleIds(user.Guild.Id));
        }
        catch (Exception ex)
        {
            _logger.ForContext("GuildId", user.Guild.Id).Warning(ex, "Failed to update quorum member index after member joined.");
        }

        return Task.CompletedTask;
    }

    private Task HandleUserLeftAsync(SocketGuild guild, SocketUser user)
    {
        try
        {
            RemoveUserFromGuild(guild.Id, user.Id);
        }
        catch (Exception ex)
        {
            _logger.ForContext("GuildId", guild.Id).Warning(ex, "Failed to update quorum member index after member left.");
        }

        return Task.CompletedTask;
    }

    private Task HandleGuildMemberUpdatedAsync(Cacheable<SocketGuildUser, ulong> before, SocketGuildUser after)
    {
        try
        {
            RemoveUserFromGuild(after.Guild.Id, after.Id);
            IndexUserForRoles(after, GetTrackedRoleIds(after.Guild.Id));
        }
        catch (Exception ex)
        {
            _logger.ForContext("GuildId", after.Guild.Id).Warning(ex, "Failed to update quorum member index after member update.");
        }

        return Task.CompletedTask;
    }

    private Task HandleRoleDeletedAsync(SocketRole role)
    {
        try
        {
            RemoveRole(role.Guild.Id, role.Id);
        }
        catch (Exception ex)
        {
            _logger.ForContext("GuildId", role.Guild.Id).Warning(ex, "Failed to update quorum member index after role deletion.");
        }

        return Task.CompletedTask;
    }

    private ImmutableHashSet<ulong> GetMissingRoleIds(ulong guildId, ImmutableHashSet<ulong> roleIds)
    {
        lock (_lock)
        {
            if (!_guilds.TryGetValue(guildId, out GuildIndex? guild))
                return roleIds;

            return roleIds.Where(roleId => !guild.WarmedRoleIds.Contains(roleId)).ToImmutableHashSet();
        }
    }

    private ImmutableHashSet<ulong> GetTrackedRoleIds(ulong guildId)
    {
        lock (_lock)
        {
            return _guilds.TryGetValue(guildId, out GuildIndex? guild) ? guild.RoleMembers.Keys.ToImmutableHashSet() : ImmutableHashSet<ulong>.Empty;
        }
    }

    private void PrepareRoleSetsForWarmup(ulong guildId, ImmutableHashSet<ulong> roleIds)
    {
        lock (_lock)
        {
            GuildIndex guild = GetOrCreateGuildIndex(guildId);

            foreach (ulong roleId in roleIds)
                guild.RoleMembers[roleId] = [];
        }
    }

    private void MarkRolesReady(ulong guildId, ImmutableHashSet<ulong> roleIds)
    {
        lock (_lock)
        {
            GuildIndex guild = GetOrCreateGuildIndex(guildId);

            foreach (ulong roleId in roleIds)
                guild.WarmedRoleIds.Add(roleId);
        }
    }

    private void IndexUserForRoles(IGuildUser user, ImmutableHashSet<ulong> roleIds)
    {
        if (roleIds.IsEmpty || user.IsBot)
            return;

        ImmutableArray<ulong> matchingRoleIds = user.RoleIds.Where(roleIds.Contains).ToImmutableArray();

        if (matchingRoleIds.IsEmpty)
            return;

        lock (_lock)
        {
            GuildIndex guild = GetOrCreateGuildIndex(user.GuildId);

            foreach (ulong roleId in matchingRoleIds)
            {
                if (guild.RoleMembers.TryGetValue(roleId, out HashSet<ulong>? userIds))
                    userIds.Add(user.Id);
            }
        }
    }

    private void RemoveUserFromGuild(ulong guildId, ulong userId)
    {
        lock (_lock)
        {
            if (!_guilds.TryGetValue(guildId, out GuildIndex? guild))
                return;

            foreach (HashSet<ulong> userIds in guild.RoleMembers.Values)
                userIds.Remove(userId);
        }
    }

    private void RemoveRole(ulong guildId, ulong roleId)
    {
        lock (_lock)
        {
            if (!_guilds.TryGetValue(guildId, out GuildIndex? guild))
                return;

            guild.WarmedRoleIds.Remove(roleId);
            guild.RoleMembers.Remove(roleId);
        }
    }

    private GuildIndex GetOrCreateGuildIndex(ulong guildId)
    {
        if (_guilds.TryGetValue(guildId, out GuildIndex? guild))
            return guild;

        guild = new GuildIndex();
        _guilds.Add(guildId, guild);

        return guild;
    }

    private sealed class GuildIndex
    {
        public HashSet<ulong> WarmedRoleIds { get; } = [];
        public Dictionary<ulong, HashSet<ulong>> RoleMembers { get; } = [];
    }
}
