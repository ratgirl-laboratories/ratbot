using RatBot.Domain.Entities;
using RatBot.Infrastructure.Services;

namespace RatBot.Discord;

public sealed class VirtueModule
{
    private const int MaxConcurrentVirtueEventWork = 8;

    private readonly DiscordSocketClient _discordClient;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;
    private readonly IReadOnlyList<VirtueRoleTier> _fallbackRoleTiers;
    private readonly ulong _fallbackBaselineRoleId;
    private readonly SemaphoreSlim _eventWorkGate = new(MaxConcurrentVirtueEventWork, MaxConcurrentVirtueEventWork);

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

        _fallbackRoleTiers = LoadRoleTiers(config).OrderBy(x => x.MinVirtue).Take(6).ToList();
        _fallbackBaselineRoleId = ParseUlong(config["Virtue:BaselineRoleId"]);

        if (_fallbackRoleTiers.Count != 6)
            _logger.Warning(
                "Expected 6 fallback configured virtue role tiers, but loaded {TierCount}.",
                _fallbackRoleTiers.Count
            );
    }

    public void RegisterHandlers()
    {
        if (_isRegistered)
            return;

        _discordClient.ReactionAdded += OnReactionAddedAsync;
        _discordClient.MessageReceived += OnMessageReceivedAsync;

        _isRegistered = true;
    }

    private Task OnMessageReceivedAsync(SocketMessage rawMessage)
    {
        _ = QueueVirtueEventWorkAsync(
            () => HandleMessageReceivedAsync(rawMessage),
            "message"
        );

        return Task.CompletedTask;
    }

    private async Task HandleMessageReceivedAsync(SocketMessage rawMessage)
    {
        if (rawMessage is not SocketUserMessage message)
            return;

        if (message.Author.IsBot)
            return;

        if (message.Channel is not SocketGuildChannel guildChannel)
            return;

        if (message.Author is not SocketGuildUser author)
            return;

        try
        {
            await using AsyncServiceScope scope = _services.CreateAsyncScope();
            VirtueRoleTierConfigService configService =
                scope.ServiceProvider.GetRequiredService<VirtueRoleTierConfigService>();
            UserVirtueService userVirtueService = scope.ServiceProvider.GetRequiredService<UserVirtueService>();
            int virtue = await userVirtueService.GetVirtueAsync(author.Id);
            GuildRoleAssignmentConfig assignmentConfig = await ResolveRoleAssignmentConfigAsync(
                guildChannel.Guild.Id,
                configService
            );
            await ApplyRoleAssignmentAsync(author, virtue, assignmentConfig);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to assign virtue role on message for user {UserId}.", author.Id);
        }
    }

    private Task OnReactionAddedAsync(
        Cacheable<IUserMessage, ulong> cachedMessage,
        Cacheable<IMessageChannel, ulong> cachedChannel,
        SocketReaction reaction
    )
    {
        _ = QueueVirtueEventWorkAsync(
            () => HandleReactionAddedAsync(cachedMessage, cachedChannel, reaction),
            "reaction"
        );

        return Task.CompletedTask;
    }

    private async Task HandleReactionAddedAsync(
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
            if (message is null)
                return;

            string emojiId = ResolveEmojiId(reaction.Emote);

            await using AsyncServiceScope scope = _services.CreateAsyncScope();
            EmojiUsageService emojiUsageService = scope.ServiceProvider.GetRequiredService<EmojiUsageService>();
            await emojiUsageService.IncrementUsageAsync(emojiId);

            if (message.Author.IsBot)
                return;

            EmojiVirtueService emojiVirtueService = scope.ServiceProvider.GetRequiredService<EmojiVirtueService>();
            int? virtueDelta = await emojiVirtueService.GetVirtueAsync(emojiId);
            if (virtueDelta is null)
                return;

            VirtueRoleTierConfigService configService = scope.ServiceProvider.GetRequiredService<VirtueRoleTierConfigService>();
            UserVirtueService userVirtueService = scope.ServiceProvider.GetRequiredService<UserVirtueService>();
            int updatedVirtue = await userVirtueService.AddVirtueDeltaAsync(message.Author.Id, virtueDelta.Value);
            int previousVirtue = updatedVirtue - virtueDelta.Value;

            _logger.Information(
                "Virtue changed for user {UserId} in guild {GuildId}: {PreviousVirtue} -> {UpdatedVirtue} (delta {VirtueDelta}) via emoji {EmojiId}, reactor {ReactorUserId}, message {MessageId}",
                message.Author.Id,
                guildChannel.Guild.Id,
                previousVirtue,
                updatedVirtue,
                virtueDelta.Value,
                emojiId,
                reaction.UserId,
                message.Id
            );

            SocketGuild guild = guildChannel.Guild;
            SocketGuildUser? author = message.Author as SocketGuildUser ?? guild.GetUser(message.Author.Id);

            if (author is null || author.IsBot)
                return;

            GuildRoleAssignmentConfig assignmentConfig = await ResolveRoleAssignmentConfigAsync(guild.Id, configService);
            await ApplyRoleAssignmentAsync(author, updatedVirtue, assignmentConfig);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed processing virtue reaction event.");
        }
    }

    private async Task QueueVirtueEventWorkAsync(Func<Task> work, string source)
    {
        try
        {
            await _eventWorkGate.WaitAsync();

            try
            {
                await work();
            }
            finally
            {
                _eventWorkGate.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unhandled virtue background processing error for {Source}.", source);
        }
    }

    private async Task ApplyRoleAssignmentAsync(SocketGuildUser user, int virtue, GuildRoleAssignmentConfig config)
    {
        VirtueRoleTier? matchedTier = config.RoleTiers.FirstOrDefault(x =>
            x.Contains(virtue) && user.Guild.GetRole(x.RoleId) is not null
        );
        ulong targetRoleId = matchedTier?.RoleId ?? config.FallbackBaselineRoleId;

        if (targetRoleId == 0)
            return;

        List<SocketRole> trackedRoles = config.RoleTiers
            .Select(x => user.Guild.GetRole(x.RoleId))
            .Where(x => x is not null)
            .ToList();

        SocketRole? baselineRole = user.Guild.GetRole(config.FallbackBaselineRoleId);
        if (baselineRole is not null && trackedRoles.All(x => x.Id != baselineRole.Id))
            trackedRoles.Add(baselineRole);

        List<IRole> toAdd = [];
        List<IRole> toRemove = [];

        foreach (SocketRole role in trackedRoles)
        {
            bool userHasRole = user.Roles.Any(r => r.Id == role.Id);
            bool shouldHaveRole = role.Id == targetRoleId;

            switch (shouldHaveRole)
            {
                case true when !userHasRole:
                    toAdd.Add(role);
                    break;
                case false when userHasRole:
                    toRemove.Add(role);
                    break;
            }
        }

        if (toRemove.Count > 0)
            await user.RemoveRolesAsync(toRemove);

        if (toAdd.Count > 0)
        {
            await user.AddRolesAsync(toAdd);

            foreach (IRole role in toAdd)
                _logger.Information(
                    "Granted virtue role {RoleId} ({RoleName}) to user {UserId} in guild {GuildId} at virtue {Virtue}",
                    role.Id,
                    role.Name,
                    user.Id,
                    user.Guild.Id,
                    virtue
                );
        }
    }

    private static List<VirtueRoleTier> LoadRoleTiers(IConfiguration config)
    {
        List<VirtueRoleTier> tiers = [];

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

    private async Task<GuildRoleAssignmentConfig> ResolveRoleAssignmentConfigAsync(
        ulong guildId,
        VirtueRoleTierConfigService configService
    )
    {
        List<VirtueRoleTierConfig> persisted = await configService.ListAsync(guildId);

        if (persisted.Count == 7)
        {
            IReadOnlyList<VirtueRoleTier> tiers = persisted
                .OrderBy(x => x.TierIndex)
                .Select(x => new VirtueRoleTier(x.RoleId, x.MinVirtue, x.MaxVirtue))
                .ToList();

            return new GuildRoleAssignmentConfig(tiers, 0);
        }

        return new GuildRoleAssignmentConfig(_fallbackRoleTiers, _fallbackBaselineRoleId);
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

    private sealed record GuildRoleAssignmentConfig(IReadOnlyList<VirtueRoleTier> RoleTiers, ulong FallbackBaselineRoleId);
}
