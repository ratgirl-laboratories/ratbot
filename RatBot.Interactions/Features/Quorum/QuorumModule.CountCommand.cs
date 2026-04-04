using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using RatBot.Domain.Entities;
using RatBot.Domain.Enums;
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

        QuorumScopeConfig? config = await _quorumConfigService.GetAsync(currentChannel.GuildId, QuorumScopeType.Channel, currentChannel.Id);

        if (config is null && category?.Id is { } categoryId)
            config = await _quorumConfigService.GetAsync(currentChannel.GuildId, QuorumScopeType.Category, categoryId);

        if (config is null)
            return InteractionResponse.Ephemeral("No quorum config found for this channel or category. Please use `/quorum config add` to add one.");

        SocketGuild guild = Context.Guild!;

        SocketRole[] roles = config.RoleIds.Select(guild.GetRole).Where(role => role is not null).ToArray();

        int membersWithRole = roles.SelectMany(role => role.Members).Select(member => member.Id).Distinct().Count();

        int quorumCount = (int)Math.Ceiling(membersWithRole * config.QuorumProportion);

        _logger.Debug(
            "Members with roles {RoleIds}: {MembersWithRole}, quorum count: {QuorumCount}, proportion: {ConfigQuorumProportion}",
            config.RoleIds,
            membersWithRole,
            quorumCount,
            config.QuorumProportion
        );

        return InteractionResponse.Public($"Quorum count for {currentChannel.Mention}: {quorumCount}");
    }
}
