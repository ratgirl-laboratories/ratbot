using System.Diagnostics;
using RatBot.Domain.Modules.Quorum;

namespace RatBot.Infrastructure.Persistence.Quorum;

internal static class QuorumPersistenceMapping
{
    private const string TextChannel = "text";
    private const string ForumChannel = "forum";

    public static QuorumConfigurationRow NewRow(QuorumScope scope, QuorumProportion proportion) =>
        new QuorumConfigurationRow
        {
            Id = QuorumConfigurationId.New().Value,
            GuildId = scope.GuildId,
            ChannelId = scope.ChannelId,
            ChannelKind = ToDiscriminator(scope),
            Proportion = proportion.Value,
        };

    public static QuorumConfiguration ToDomain(QuorumConfigurationRow row)
    {
        QuorumScope scope = ToScope(row);

        ErrorOr<QuorumProportion> proportion = QuorumProportion.Create(row.Proportion);

        if (proportion.IsError)
            throw new InvalidOperationException("Persisted quorum proportion is invalid.");

        return QuorumConfiguration.Rehydrate(
            new QuorumConfigurationId(row.Id),
            scope,
            proportion.Value,
            new QuorumVoterRoleSet(row.VoterRoles.Select(role => role.RoleId))
        );
    }

    private static string ToDiscriminator(QuorumScope scope) =>
        scope switch
        {
            QuorumScope.TextChannel => TextChannel,
            QuorumScope.ForumChannel => ForumChannel,
            _ => throw new UnreachableException(),
        };

    private static QuorumScope ToScope(QuorumConfigurationRow row) =>
        row.ChannelKind switch
        {
            TextChannel => new QuorumScope.TextChannel(row.GuildId, row.ChannelId),
            ForumChannel => new QuorumScope.ForumChannel(row.GuildId, row.ChannelId),
            _ => throw new InvalidOperationException($"Unknown quorum channel kind '{row.ChannelKind}'."),
        };
}