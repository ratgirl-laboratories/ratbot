namespace RatBot.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for RatBot persistence.
/// </summary>
public sealed class BotDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BotDbContext"/> class.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    public BotDbContext(DbContextOptions<BotDbContext> options)
        : base(options) { }

    /// <summary>
    /// Gets the persisted config entry set.
    /// </summary>
    public DbSet<ConfigEntry> ConfigEntries => Set<ConfigEntry>();

    /// <summary>
    /// Gets the emoji usage set.
    /// </summary>
    public DbSet<EmojiUsageCount> EmojiUsageCounts => Set<EmojiUsageCount>();

    /// <summary>
    /// Configures entity mappings.
    /// </summary>
    /// <param name="modelBuilder">The model builder instance.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfigEntry>(b =>
        {
            b.ToTable("Configs");
            b.HasKey(x => new { x.Key, x.Value });
            b.Property(x => x.Key).HasColumnType("text");
            b.Property(x => x.Value).HasColumnType("jsonb");
        });

        modelBuilder.Entity<EmojiUsageCount>(b =>
        {
            b.HasKey(x => x.EmojiId);

            b.Property(x => x.EmojiId).HasMaxLength(128);
            b.Property(x => x.UsageCount).HasColumnType("int");
        });
    }
}
