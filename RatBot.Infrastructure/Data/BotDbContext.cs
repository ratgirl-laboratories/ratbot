using RatBot.Application.Common;
using RatBot.Application.Common.Extensions;
using RatBot.Application.Common.Interfaces;
using RatBot.Application.Reactions;

namespace RatBot.Infrastructure.Data;

/// <summary>
///     Entity Framework Core database context for RatBot persistence.
/// </summary>
public sealed class BotDbContext(DbContextOptions<BotDbContext> options)
    : DbContext(options), IUnitOfWork, IRepository<MetaSuggestionSettings>, IEmojiRepository, IMetaProposalRepository
{
    public DbSet<QuorumSettings> QuorumSettings => Set<QuorumSettings>();
    public DbSet<QuorumSettingsRole> QuorumSettingsRoles => Set<QuorumSettingsRole>();
    public DbSet<EmojiUsageCount> EmojiUsageCounts => Set<EmojiUsageCount>();
    public DbSet<MetaSuggestionSettings> MetaSuggestionSettings => Set<MetaSuggestionSettings>();
    public DbSet<MetaProposalState> MetaProposalStates => Set<MetaProposalState>();
    public DbSet<AutobannedUser> AutobannedUsers => Set<AutobannedUser>();
    public DbSet<ImageSpamSettings> ImageSpamSettings => Set<ImageSpamSettings>();
    public DbSet<RoleColourOption> RoleColourOptions => Set<RoleColourOption>();
    public DbSet<MemberColourPreference> MemberColourPreferences => Set<MemberColourPreference>();
    public DbSet<AdventureForumThreadLink> AdventureForumThreadLinks => Set<AdventureForumThreadLink>();
    public DbSet<AdventureLeaderboardMessageState> AdventureLeaderboardMessageState =>
        Set<AdventureLeaderboardMessageState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<ulong>().HaveConversion<long>();
        configurationBuilder.Properties<ulong?>().HaveConversion<long?>();
    }

    #region Aggregates

    #region MetaProposal

    public void Add(MetaSuggestionSettings aggregate) => MetaSuggestionSettings.Add(aggregate);
    public void Delete(MetaSuggestionSettings aggregate) => MetaSuggestionSettings.Remove(aggregate);

    Task<ErrorOr<MetaSuggestionSettings>> IRepository<MetaSuggestionSettings>.TryFindAsync(long id) =>
        MetaSuggestionSettings
            .FirstOrDefaultAsync(s => s.GuildId == (ulong)id)
            .ToErrorOr(Error.NotFound("MetaSuggestionSettings.NotFound", $"Meta suggest settings for Guild {id}"));

    public Task<MetaProposalState?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        MetaProposalStates.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<MetaProposalState?> FindBySuggestionThreadAsync(
        ulong suggestionThreadChannelId,
        CancellationToken ct = default) =>
        MetaProposalStates.FirstOrDefaultAsync(x => x.SuggestionThreadChannelId == suggestionThreadChannelId, ct);

    public Task<MetaProposalState?> FindByProposalThreadAsync(ulong threadChannelId, CancellationToken ct = default) =>
        MetaProposalStates.FirstOrDefaultAsync(
            x => x.SuggestionThreadChannelId == threadChannelId
                 || x.ProposalThreadChannelId == threadChannelId,
            ct);

    public Task<MetaProposalState?> FindByPollMessageAsync(ulong pollMessageId, CancellationToken ct = default) =>
        MetaProposalStates.FirstOrDefaultAsync(x => x.PollMessageId == pollMessageId, ct);

    public async Task<IReadOnlyList<MetaProposalState>> FindExpiredPollsAsync(
        DateTimeOffset nowUtc,
        int limit,
        CancellationToken ct = default) =>
        await MetaProposalStates
            .Where(x =>
                x.Status == MetaProposalStatus.PollActive
                && x.PollExpiresAtUtc != null
                && x.PollExpiresAtUtc <= nowUtc
                && x.PollMessageId != null)
            .OrderBy(x => x.PollExpiresAtUtc)
            .Take(limit)
            .ToListAsync(ct);

    public void Add(MetaProposalState state) => MetaProposalStates.Add(state);
    public void Delete(MetaProposalState state) => MetaProposalStates.Remove(state);

    #endregion

    #endregion

    public IRepository<TAggregate> GetRepository<TAggregate>() =>
        this as IRepository<TAggregate>
        ?? throw new NotSupportedException(
            $"Repository for aggregate type {typeof(TAggregate).Name} is not supported by {nameof(BotDbContext)}.");
}
