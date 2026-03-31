using RatBot.Infrastructure.Services;

namespace RatBot.Discord;

public sealed class VirtueModule
{
    private readonly DiscordSocketClient _discordClient;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;
    private readonly IReadOnlyList<VirtueRoleTier> _roleTiers;
    private readonly ulong _baselineRoleId;

    private bool _isRegistered;

    public VirtueModule(
        DiscordSocketClient discordClient,
        IServiceProvider services,
        IConfiguration config,
        ILogger logger
    )
    {
        _discordClient = discordClient;
        _services = services;
        _logger = logger.ForContext<VirtueModule>();

        _roleTiers = LoadRoleTiers(config).OrderBy(x => x.MinVirtue).Take(6).ToList();
        _baselineRoleId = ParseUlong(config["Virtue:BaselineRoleId"]);

        if (_roleTiers.Count != 6)
            _logger.Warning(
                "Expected 6 configured virtue role tiers, but loaded {TierCount}.",
                _roleTiers.Count
            );
    }

    public void RegisterHandlers()
    {
        if (_isRegistered)
            return;

        _discordClient.ReactionAdded += OnReactionAddedAsync;
        _discordClient.Ready += OnReadyAsync;
        _discordClient.UserJoined += OnUserJoinedAsync;

        _isRegistered = true;
    }

    private async Task OnReadyAsync()
    {
        if (_baselineRoleId == 0)
        {
            _logger.Warning("Virtue baseline role is not configured. Set Virtue:BaselineRoleId.");
            return;
        }

        foreach (SocketGuild guild in _discordClient.Guilds)
        {
            try
            {
                await guild.DownloadUsersAsync();
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to download users for guild {GuildId}.", guild.Id);
            }

            List<SocketGuildUser> users = guild.Users.Where(x => !x.IsBot).ToList();
            await using AsyncServiceScope scope = _services.CreateAsyncScope();
            UserVirtueService userVirtueService = scope.ServiceProvider.GetRequiredService<UserVirtueService>();
            Dictionary<ulong, int> virtues = await userVirtueService.GetVirtuesAsync(users.Select(x => x.Id));

            foreach (SocketGuildUser user in users)
            {
                try
                {
                    int virtue = virtues.GetValueOrDefault(user.Id);
                    await ApplyRoleAssignmentAsync(user, virtue);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to assign virtue role for user {UserId}.", user.Id);
                }
            }
        }
    }

    private async Task OnUserJoinedAsync(SocketGuildUser user)
    {
        if (user.IsBot)
            return;

        try
        {
            await using AsyncServiceScope scope = _services.CreateAsyncScope();
            UserVirtueService userVirtueService = scope.ServiceProvider.GetRequiredService<UserVirtueService>();
            int virtue = await userVirtueService.GetVirtueAsync(user.Id);
            await ApplyRoleAssignmentAsync(user, virtue);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to assign baseline/virtue role for new user {UserId}.", user.Id);
        }
    }

    private async Task OnReactionAddedAsync(
        Cacheable<IUserMessage, ulong> cachedMessage,
        Cacheable<IMessageChannel, ulong> cachedChannel,
        SocketReaction reaction
    )
    {
        try
        {
            IMessageChannel? channel = await cachedChannel.GetOrDownloadAsync();
            if (channel is not SocketGuildChannel guildChannel)
                return;

            IUserMessage? message = await cachedMessage.GetOrDownloadAsync();
            if (message is null || message.Author.IsBot)
                return;

            string emojiId = ResolveEmojiId(reaction.Emote);

            await using AsyncServiceScope scope = _services.CreateAsyncScope();
            EmojiVirtueService emojiVirtueService = scope.ServiceProvider.GetRequiredService<EmojiVirtueService>();
            int? virtueDelta = await emojiVirtueService.GetVirtueAsync(emojiId);
            if (virtueDelta is null)
                return;

            UserVirtueService userVirtueService = scope.ServiceProvider.GetRequiredService<UserVirtueService>();
            int updatedVirtue = await userVirtueService.AddVirtueDeltaAsync(message.Author.Id, virtueDelta.Value);

            SocketGuild guild = guildChannel.Guild;
            SocketGuildUser? author = message.Author as SocketGuildUser ?? guild.GetUser(message.Author.Id);

            if (author is null || author.IsBot)
                return;

            await ApplyRoleAssignmentAsync(author, updatedVirtue);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed processing virtue reaction event.");
        }
    }

    private async Task ApplyRoleAssignmentAsync(SocketGuildUser user, int virtue)
    {
        if (_baselineRoleId == 0)
            return;

        SocketRole? baselineRole = user.Guild.GetRole(_baselineRoleId);
        if (baselineRole is null)
            return;

        VirtueRoleTier? matchedTier = _roleTiers.FirstOrDefault(x =>
            x.Contains(virtue) && user.Guild.GetRole(x.RoleId) is not null
        );
        ulong targetRoleId = matchedTier?.RoleId ?? _baselineRoleId;

        List<SocketRole> trackedRoles = _roleTiers
            .Select(x => user.Guild.GetRole(x.RoleId))
            .Where(x => x is not null)
            .Cast<SocketRole>()
            .ToList();

        if (trackedRoles.All(x => x.Id != baselineRole.Id))
            trackedRoles.Add(baselineRole);

        List<IRole> toAdd = new List<IRole>();
        List<IRole> toRemove = new List<IRole>();

        foreach (SocketRole role in trackedRoles)
        {
            bool userHasRole = user.Roles.Any(r => r.Id == role.Id);
            bool shouldHaveRole = role.Id == targetRoleId;

            if (shouldHaveRole && !userHasRole)
                toAdd.Add(role);
            else if (!shouldHaveRole && userHasRole)
                toRemove.Add(role);
        }

        if (toRemove.Count > 0)
            await user.RemoveRolesAsync(toRemove);

        if (toAdd.Count > 0)
            await user.AddRolesAsync(toAdd);
    }

    private static List<VirtueRoleTier> LoadRoleTiers(IConfiguration config)
    {
        List<VirtueRoleTier> tiers = new List<VirtueRoleTier>();

        foreach (IConfigurationSection child in config.GetSection("Virtue:RoleTiers").GetChildren())
        {
            ulong roleId = ParseUlong(child["RoleId"]);
            if (roleId == 0)
                continue;

            if (
                !int.TryParse(child["MinVirtue"], out int minVirtue)
                || !int.TryParse(child["MaxVirtue"], out int maxVirtue)
            )
                continue;

            tiers.Add(new VirtueRoleTier(roleId, minVirtue, maxVirtue));
        }

        return tiers;
    }

    private static ulong ParseUlong(string? value)
    {
        return ulong.TryParse(value, out ulong parsed) ? parsed : 0;
    }

    private static string ResolveEmojiId(IEmote emote)
    {
        return emote switch
        {
            Emote customEmoji => customEmoji.Id.ToString(),
            Emoji unicodeEmoji => unicodeEmoji.Name,
            _ => emote.Name,
        };
    }

    private sealed record VirtueRoleTier(ulong RoleId, int MinVirtue, int MaxVirtue)
    {
        public bool Contains(int value) => value >= MinVirtue && value <= MaxVirtue;
    }
}
