using JetBrains.Annotations;
using RatBot.Interactions.Common.Responses;

namespace RatBot.Interactions.Modules.Quorum;

[Group("quorum", "Quorum commands. Group restricted to moderators by default.")]
[DefaultMemberPermissions(GuildPermission.MuteMembers)]
public sealed class QuorumModule(ILogger logger, QuorumConfigurationService quorumConfigurationService)
    : SlashCommandBase
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

        QuorumConfig? config = await quorumConfigurationService.GetEffectiveAsync(
            currentChannel.GuildId,
            currentChannel.Id,
            category?.Id);

        if (config is null)
            return InteractionResponse.Ephemeral(
                "No quorum configuration found for this channel or category. Please use `/config quorum set` to configure one.");

        SocketGuild guild = Context.Guild!;
        SocketRole[] roles = config.RoleIds.Select(guild.GetRole).Where(role => role is not null).ToArray()!;
        int membersWithRole = roles.SelectMany(role => role.Members).Select(member => member.Id).Distinct().Count();
        int quorumCount = QuorumCalculator.CalculateRequiredMemberCount(membersWithRole, config.QuorumProportion);

        _logger.Debug(
            "Members with roles {RoleIds}: {MembersWithRole}, quorum count: {QuorumCount}, proportion: {ConfigQuorumProportion}",
            config.RoleIds,
            membersWithRole,
            quorumCount,
            config.QuorumProportion);

        return InteractionResponse.Public($"Quorum count for {currentChannel.Mention}: {quorumCount}");
    }
}