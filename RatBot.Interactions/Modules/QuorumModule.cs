using RatBot.Interactions.Common.Responses;

namespace RatBot.Interactions.Modules.Quorum;

[Group("quorum", "Quorum commands. Group restricted to moderators by default.")]
[DefaultMemberPermissions(GuildPermission.MuteMembers)]
public sealed class QuorumModule(ILogger logger, QuorumSettingsService quorumSettingsService) : SlashCommandBase
{
    private readonly ILogger _logger = logger.ForContext<QuorumModule>();

    [UsedImplicitly]
    [SlashCommand("count", "Count the number of members needed for quorum.")]
    [RequireUserPermission(GuildPermission.SendPolls)]
    public Task CountAsync() => ReplyAsync(CountResponseAsync);

    private async Task<InteractionResponse> CountResponseAsync()
    {
        if (Context.Channel is not ITextChannel currentChannel)
            return InteractionResponse.Ephemeral("This command can only be used in a text channel.");

        ICategoryChannel? category = await currentChannel.GetCategoryAsync();

        QuorumSettings? config = await quorumSettingsService.GetEffectiveAsync(
            currentChannel.GuildId,
            currentChannel.Id,
            category?.Id);

        if (config is null)
            return InteractionResponse.Ephemeral(
                "No quorum settings found for this channel or category. Please use `/config quorum set` to configure one.");

        logger.Debug("Quorum settings: {Config}", config);

        SocketGuild guild = Context.Guild!;
        SocketRole[] roles = config.RoleIds.Select(guild.GetRole).Where(role => role is not null).ToArray()!;

        HashSet<ulong> usersWithRoles = roles.SelectMany(x => x.Members).Select(y => y.Id).ToHashSet();

        int quorumCount = QuorumCalculator.CalculateRequiredMemberCount(usersWithRoles.Count, config.QuorumProportion);

        _logger.Debug(
            "Members with roles {RoleIds}: {UsersWithRoles}, quorum count: {QuorumCount}, proportion: {ConfigQuorumProportion}",
            config.RoleIds,
            usersWithRoles,
            quorumCount,
            config.QuorumProportion);

        return InteractionResponse.Public($"Quorum count for {currentChannel.Mention}: {quorumCount}");
    }
}