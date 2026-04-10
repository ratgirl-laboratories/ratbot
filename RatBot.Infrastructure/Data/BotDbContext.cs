using RatBot.Infrastructure.Configuration.Quorum;

namespace RatBot.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for RatBot persistence.
/// </summary>
public sealed class BotDbContext : DbContext, IBotDataContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BotDbContext"/> class.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    public BotDbContext(DbContextOptions<BotDbContext> options)
        : base(options) { }

    public DbSet<EmojiUsageCount> EmojiUsageCounts => Set<EmojiUsageCount>();

    public DbSet<QuorumConfigEntity> QuorumConfigs =>
        Set<QuorumConfigEntity>();

    public DbSet<QuorumConfigRoleEntity> QuorumConfigRoles =>
        Set<QuorumConfigRoleEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmojiUsageCount>(b =>
        {
            b.HasKey(x => x.EmojiId);

            b.Property(x => x.EmojiId).HasMaxLength(128);
            b.Property(x => x.UsageCount).HasColumnType("int");
        });

        modelBuilder.Entity<QuorumConfigEntity>(b =>
        {
            b.ToTable("QuorumConfigs");
            b.HasKey(x => new { x.GuildId, x.TargetType, x.TargetId });

            b.Property(x => x.GuildId).HasColumnType("numeric(20,0)");
            b.Property(x => x.TargetType).HasColumnType("integer");
            b.Property(x => x.TargetId).HasColumnType("numeric(20,0)");
            b.Property(x => x.QuorumProportion).HasColumnType("double precision").HasPrecision(6, 4);

            b.HasIndex(x => x.GuildId);
            b.HasIndex(x => new { x.GuildId, x.TargetType });

            b.HasMany(x => x.Roles)
                .WithOne()
                .HasForeignKey("GuildId", "TargetType", "TargetId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuorumConfigRoleEntity>(b =>
        {
            b.ToTable("QuorumConfigRoles");
            b.Property<ulong>("GuildId").HasColumnType("numeric(20,0)");
            b.Property<QuorumConfigType>("TargetType").HasColumnType("integer");
            b.Property<ulong>("TargetId").HasColumnType("numeric(20,0)");
            b.Property(x => x.RoleId).HasColumnType("numeric(20,0)");

            b.HasKey("GuildId", "TargetType", "TargetId", "RoleId");
        });
    }
}
