using RatBot.Application.Common;
using RatBot.Application.Common.Extensions;
using RatBot.Application.Common.Interfaces;
using RatBot.Application.Reactions;

namespace RatBot.Infrastructure.Data;

/// <summary>
///     Entity Framework Core database context for RatBot persistence.
/// </summary>
public sealed class BotDbContext(DbContextOptions<BotDbContext> options)
    : DbContext(options), IUnitOfWork, IRepository<MetaSuggestion>, IRepository<MetaSuggestionSettings>, IEmojiRepository
{
    public DbSet<QuorumSettings> QuorumSettings => Set<QuorumSettings>();
    public DbSet<QuorumSettingsRole> QuorumSettingsRoles => Set<QuorumSettingsRole>();
    public DbSet<EmojiUsageCount> EmojiUsageCounts => Set<EmojiUsageCount>();
    public DbSet<MetaSuggestionSettings> MetaSuggestionSettings => Set<MetaSuggestionSettings>();
    public DbSet<AutobannedUser> AutobannedUsers => Set<AutobannedUser>();
    public DbSet<RoleColourOption> RoleColourOptions => Set<RoleColourOption>();
    public DbSet<MemberColourPreference> MemberColourPreferences => Set<MemberColourPreference>();
    public DbSet<AdventureForumThreadLink> AdventureForumThreadLinks => Set<AdventureForumThreadLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<ulong>().HaveConversion<long>();
        configurationBuilder.Properties<ulong?>().HaveConversion<long?>();
    }

    #region Aggregates

    #region MetaSuggestions

    public DbSet<MetaSuggestion> MetaSuggestions => Set<MetaSuggestion>();
    public void Add(MetaSuggestion aggregate) => MetaSuggestions.Add(aggregate);
    public void Delete(MetaSuggestion aggregate) => MetaSuggestions.Remove(aggregate);

    public Task<ErrorOr<MetaSuggestion>> TryFindAsync(long id) =>
        MetaSuggestions
            .FirstOrDefaultAsync(s => s.Id == id)
            .ToErrorOr(Error.NotFound("MetaSuggestion.NotFound", $"Suggestion {id} not found."));

    public void Add(MetaSuggestionSettings aggregate) => MetaSuggestionSettings.Add(aggregate);
    public void Delete(MetaSuggestionSettings aggregate) => MetaSuggestionSettings.Remove(aggregate);

    Task<ErrorOr<MetaSuggestionSettings>> IRepository<MetaSuggestionSettings>.TryFindAsync(long id) =>
        MetaSuggestionSettings
            .FirstOrDefaultAsync(s => s.GuildId == (ulong)id)
            .ToErrorOr(Error.NotFound("MetaSuggestionSettings.NotFound", $"Meta suggest settings for Guild {id}"));

    #endregion

    #endregion

    public IRepository<TAggregate> GetRepository<TAggregate>() =>
        this as IRepository<TAggregate>
        ?? throw new NotSupportedException(
            $"Repository for aggregate type {typeof(TAggregate).Name} is not supported by {nameof(BotDbContext)}.");
}
