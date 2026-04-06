using LanguageExt;
using RatBot.Interactions.Common.Responses;

namespace RatBot.Interactions.Features.Quorum;

public sealed partial class QuorumModule
{
    /// <summary>
    /// Counts members required to satisfy the current channel or category quorum configuration.
    /// </summary>
    [SlashCommand("count", "Count the number of members needed for quorum.")]
    [RequireUserPermission(GuildPermission.SendPolls)]
    public Task CountAsync()
    {
        return ReplyAsync(CountResponse);
    }

    private async Task<InteractionResponse> CountResponse()
    {
        if (Context.Channel is not ITextChannel currentChannel)
            return InteractionResponse.Ephemeral("This command can only be used in a text channel.");

        ICategoryChannel? category = await currentChannel.GetCategoryAsync();

        Option<QuorumScopeConfig> config = await QuorumConfigs.GetForChannelAsync(
            configRepository,
            currentChannel.GuildId,
            currentChannel.Id,
            category?.Id
        );

        return config.Match(
            resolvedConfig =>
            {
                SocketGuild guild = Context.Guild!;
                SocketRole[] roles = resolvedConfig.RoleIds.Select(guild.GetRole).Where(role => role is not null).ToArray();
                int membersWithRole = roles.SelectMany(role => role.Members).Select(member => member.Id).Distinct().Count();
                int quorumCount = (int)Math.Ceiling(membersWithRole * resolvedConfig.QuorumProportion);

                logger.Debug(
                    "Members with roles {RoleIds}: {MembersWithRole}, quorum count: {QuorumCount}, proportion: {ConfigQuorumProportion}",
                    resolvedConfig.RoleIds,
                    membersWithRole,
                    quorumCount,
                    resolvedConfig.QuorumProportion
                );

                return InteractionResponse.Public($"Quorum count for {currentChannel.Mention}: {quorumCount}");
            },
            () => InteractionResponse.Ephemeral(
                "No quorum config found for this channel or category. Please use `/config quorum set` to configure one."
            )
        );
    }
}
