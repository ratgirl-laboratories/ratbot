using RatBot.Infrastructure.Persistence.Models;

namespace RatBot.Infrastructure.Data;

/// <summary>
///     Entity Framework Core database context for RatBot persistence.
/// </summary>
public sealed class BotDbContext : DbContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="BotDbContext" /> class.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options)
    {
    }

    public DbSet<QuorumSettings> QuorumSettings => Set<QuorumSettings>();
    public DbSet<QuorumSettingsRole> QuorumSettingsRoles => Set<QuorumSettingsRole>();
    public DbSet<EmojiUsageCount> EmojiUsageCounts => Set<EmojiUsageCount>();
    public DbSet<MetaSuggestion> MetaSuggestions => Set<MetaSuggestion>();
    public DbSet<MetaSuggestionSettings> MetaSuggestionSettings => Set<MetaSuggestionSettings>();
    public DbSet<AutobannedUser> AutobannedUsers => Set<AutobannedUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BotDbContext).Assembly);
}