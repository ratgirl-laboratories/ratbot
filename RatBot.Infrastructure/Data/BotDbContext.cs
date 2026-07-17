using RatBot.Application.Common.Interfaces;

namespace RatBot.Infrastructure.Data;

/// <summary>
///     Entity Framework Core database context for RatBot persistence.
/// </summary>
public sealed class BotDbContext(DbContextOptions<BotDbContext> options) : DbContext(options), IEmojiRepository
{
    public DbSet<AdventureForumThreadLink> AdventureForumThreadLinks => Set<AdventureForumThreadLink>();

    public DbSet<AdventureLeaderboardMessageState> AdventureLeaderboardMessageState => Set<AdventureLeaderboardMessageState>();
    public DbSet<AutobannedUser> AutobannedUsers => Set<AutobannedUser>();

    public DbSet<EmojiUsageCount> EmojiUsageCounts => Set<EmojiUsageCount>();
    public DbSet<ImageSpamSettings> ImageSpamSettings => Set<ImageSpamSettings>();
    public DbSet<LoggingConfiguration> LoggingConfigurations => Set<LoggingConfiguration>();
    public DbSet<LoggingExcludedChannel> LoggingExcludedChannels => Set<LoggingExcludedChannel>();
    public DbSet<MemberColourPreference> MemberColourPreferences => Set<MemberColourPreference>();
    public DbSet<MessageLogEntry> MessageLogEntries => Set<MessageLogEntry>();
    public DbSet<MetaProposalState> MetaProposalStates => Set<MetaProposalState>();
    public DbSet<MetaSuggestionSettings> MetaSuggestionSettings => Set<MetaSuggestionSettings>();
    public DbSet<ObservedMessage> ObservedMessages => Set<ObservedMessage>();
    public DbSet<RoleColourOption> RoleColourOptions => Set<RoleColourOption>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<ulong>().HaveConversion<long>();
        configurationBuilder.Properties<ulong?>().HaveConversion<long?>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);
}
