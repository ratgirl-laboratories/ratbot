using RatBot.Application.Common.Interfaces;
using RatBot.Domain.SecretRole;

namespace RatBot.Infrastructure.Data;

/// <summary>
///     Entity Framework Core database context for RatBot persistence.
/// </summary>
public sealed class BotDbContext(DbContextOptions<BotDbContext> options) : DbContext(options), IEmojiRepository
{
    public DbSet<QuorumSettings> QuorumSettings => Set<QuorumSettings>();
    public DbSet<QuorumSettingsRole> QuorumSettingsRoles => Set<QuorumSettingsRole>();
    public DbSet<MetaSuggestionSettings> MetaSuggestionSettings => Set<MetaSuggestionSettings>();
    public DbSet<MetaProposalState> MetaProposalStates => Set<MetaProposalState>();
    public DbSet<AutobannedUser> AutobannedUsers => Set<AutobannedUser>();
    public DbSet<ImageSpamSettings> ImageSpamSettings => Set<ImageSpamSettings>();
    public DbSet<RoleColourOption> RoleColourOptions => Set<RoleColourOption>();
    public DbSet<MemberColourPreference> MemberColourPreferences => Set<MemberColourPreference>();
    public DbSet<AdventureForumThreadLink> AdventureForumThreadLinks => Set<AdventureForumThreadLink>();

    public DbSet<AdventureLeaderboardMessageState> AdventureLeaderboardMessageState => Set<AdventureLeaderboardMessageState>();

    public DbSet<EmojiUsageCount> EmojiUsageCounts => Set<EmojiUsageCount>();
    public DbSet<SecretRoleSetting> TemporaryPingRoleSettings => Set<SecretRoleSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<ulong>().HaveConversion<long>();
        configurationBuilder.Properties<ulong?>().HaveConversion<long?>();
    }
}
