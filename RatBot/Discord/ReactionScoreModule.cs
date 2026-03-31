using RatBot.Infrastructure.Services;

namespace RatBot.Discord;

public sealed class ReactionScoreModule
{
    private readonly DiscordSocketClient _discordClient;
    private readonly IServiceProvider _services;
    private readonly ILogger _logger;
    private readonly IReadOnlyList<ScoreRoleTier> _roleTiers;
    private readonly ulong _baselineRoleId;

    private bool _isRegistered;

    public ReactionScoreModule(
        DiscordSocketClient discordClient,
        IServiceProvider services,
        IConfiguration config,
        ILogger logger
    )
    {
        _discordClient = discordClient;
        _services = services;
        _logger = logger.ForContext<ReactionScoreModule>();

        _roleTiers = LoadRoleTiers(config).OrderBy(x => x.MinScore).Take(6).ToList();
        _baselineRoleId = ParseUlong(config["ReactionScore:BaselineRoleId"]);

        if (_roleTiers.Count != 6)
            _logger.Warning(
                "Expected 6 configured role tiers for ReactionScore, but loaded {TierCount}.",
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
            _logger.Warning("ReactionScore baseline role is not configured. Set ReactionScore:BaselineRoleId.");
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
            UserScoreService userScoreService = scope.ServiceProvider.GetRequiredService<UserScoreService>();
            Dictionary<ulong, int> scores = await userScoreService.GetScoresAsync(users.Select(x => x.Id));

            foreach (SocketGuildUser user in users)
            {
                try
                {
                    int score = scores.GetValueOrDefault(user.Id);
                    await ApplyRoleAssignmentAsync(user, score);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to assign score role for user {UserId}.", user.Id);
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
            UserScoreService userScoreService = scope.ServiceProvider.GetRequiredService<UserScoreService>();
            int score = await userScoreService.GetScoreAsync(user.Id);
            await ApplyRoleAssignmentAsync(user, score);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to assign baseline/score role for new user {UserId}.", user.Id);
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
            ReactionEmojiScoreService emojiScoreService = scope.ServiceProvider.GetRequiredService<ReactionEmojiScoreService>();
            int? delta = await emojiScoreService.GetScoreAsync(emojiId);
            if (delta is null)
                return;

            UserScoreService userScoreService = scope.ServiceProvider.GetRequiredService<UserScoreService>();
            int updatedScore = await userScoreService.AddDeltaAsync(message.Author.Id, delta.Value);

            SocketGuild guild = guildChannel.Guild;
            SocketGuildUser? author = message.Author as SocketGuildUser ?? guild.GetUser(message.Author.Id);

            if (author is null || author.IsBot)
                return;

            await ApplyRoleAssignmentAsync(author, updatedScore);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed processing reaction score event.");
        }
    }

    private async Task ApplyRoleAssignmentAsync(SocketGuildUser user, int score)
    {
        if (_baselineRoleId == 0)
            return;

        SocketRole? baselineRole = user.Guild.GetRole(_baselineRoleId);
        if (baselineRole is null)
            return;

        ScoreRoleTier? matchedTier = _roleTiers.FirstOrDefault(x => x.Contains(score) && user.Guild.GetRole(x.RoleId) is not null);
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

    private static List<ScoreRoleTier> LoadRoleTiers(IConfiguration config)
    {
        List<ScoreRoleTier> tiers = new List<ScoreRoleTier>();

        foreach (IConfigurationSection child in config.GetSection("ReactionScore:RoleTiers").GetChildren())
        {
            ulong roleId = ParseUlong(child["RoleId"]);
            if (roleId == 0)
                continue;

            if (!int.TryParse(child["MinScore"], out int minScore) || !int.TryParse(child["MaxScore"], out int maxScore))
                continue;

            tiers.Add(new ScoreRoleTier(roleId, minScore, maxScore));
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

    private sealed record ScoreRoleTier(ulong RoleId, int MinScore, int MaxScore)
    {
        public bool Contains(int value) => value >= MinScore && value <= MaxScore;
    }
}
