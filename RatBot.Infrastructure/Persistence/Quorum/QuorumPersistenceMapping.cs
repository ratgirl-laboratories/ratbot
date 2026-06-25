using System.Diagnostics;
using RatBot.Domain.Modules.Quorum;

namespace RatBot.Infrastructure.Persistence.Quorum;

internal static class QuorumPersistenceMapping
{
    private const string TextChannel = "text";
    private const string ForumChannel = "forum";

    internal sealed class Data
    {
        public Guid Id { get; set; }
        public long GuildId { get; set; }
        public long ChannelId { get; set; }
        public string ChannelKind { get; set; } = null!;
        public decimal Proportion { get; set; }
    }

    public static long ToDatabaseId(ulong id) => checked((long)id);

    public static string ToChannelKind(QuorumScope scope) =>
        scope switch
        {
            QuorumScope.TextChannel => TextChannel,
            QuorumScope.ForumChannel => ForumChannel,
            _ => throw new UnreachableException(),
        };

    public static QuorumConfiguration ToDomain(Data data, IEnumerable<long> roleIds)
    {
        QuorumScope scope = ToScope(data);

        ErrorOr<QuorumProportion> proportion = QuorumProportion.Create(data.Proportion);

        if (proportion.IsError)
            throw new InvalidOperationException("Persisted quorum proportion is invalid.");

        return QuorumConfiguration.Rehydrate(
            new QuorumConfigurationId(data.Id),
            scope,
            proportion.Value,
            new QuorumVoterRoleSet(roleIds.Select(ToDomainId))
        );
    }

    private static ulong ToDomainId(long id) => checked((ulong)id);

    private static QuorumScope ToScope(Data data) =>
        data.ChannelKind switch
        {
            TextChannel => new QuorumScope.TextChannel(ToDomainId(data.GuildId), ToDomainId(data.ChannelId)),
            ForumChannel => new QuorumScope.ForumChannel(ToDomainId(data.GuildId), ToDomainId(data.ChannelId)),
            _ => throw new InvalidOperationException($"Unknown quorum channel kind '{data.ChannelKind}'."),
        };
}
