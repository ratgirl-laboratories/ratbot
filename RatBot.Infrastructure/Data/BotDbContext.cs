using RatBot.Domain.Entities;

namespace RatBot.Infrastructure.Data;

public sealed class BotDbContext : DbContext
{
    public DbSet<GuildConfig> GuildConfigs => Set<GuildConfig>();
    public DbSet<QuorumScopeConfig> QuorumScopeConfigs => Set<QuorumScopeConfig>();
    public DbSet<UserVirtue> UserVirtues => Set<UserVirtue>();
    public DbSet<EmojiUsageCount> EmojiUsageCounts => Set<EmojiUsageCount>();
    public DbSet<VirtueReactionLock> VirtueReactionLocks => Set<VirtueReactionLock>();
    public DbSet<VirtueRoleTierConfig> VirtueRoleTierConfigs => Set<VirtueRoleTierConfig>();

    public BotDbContext(DbContextOptions<BotDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GuildConfig>(b =>
        {
            b.HasKey(x => x.GuildId);
            b.Property(x => x.Prefix).HasMaxLength(2);
        });

        modelBuilder.Entity<QuorumScopeConfig>(b =>
        {
            b.HasKey(x => new
            {
                x.GuildId,
                x.ScopeType,
                x.ScopeId,
            });

            b.Property(x => x.GuildId).HasColumnType("bigint unsigned");
            b.Property(x => x.ScopeId).HasColumnType("bigint unsigned");
            b.Property(x => x.RoleId).HasColumnType("bigint unsigned");
            b.Property(x => x.ScopeType).HasConversion<int>();
            b.Property(x => x.QuorumProportion).HasPrecision(6, 4);

            b.HasIndex(x => x.GuildId);
            b.HasIndex(x => new { x.GuildId, x.ScopeType });
        });

        modelBuilder.Entity<UserVirtue>(b =>
        {
            b.HasKey(x => x.UserId);

            b.Property(x => x.UserId).ValueGeneratedNever().HasColumnType("bigint unsigned");
            b.Property(x => x.Virtue).HasColumnName("Score").HasColumnType("int");
        });

        modelBuilder.Entity<EmojiUsageCount>(b =>
        {
            b.HasKey(x => x.EmojiId);

            b.Property(x => x.EmojiId).HasMaxLength(128);
            b.Property(x => x.UsageCount).HasColumnType("int");
        });

        modelBuilder.Entity<VirtueReactionLock>(b =>
        {
            b.HasKey(x => new { x.MessageId, x.ReactorUserId });

            b.Property(x => x.MessageId).HasColumnType("bigint unsigned");
            b.Property(x => x.ReactorUserId).HasColumnType("bigint unsigned");
            b.Property(x => x.TargetUserId).HasColumnType("bigint unsigned");
            b.Property(x => x.EmojiId).HasMaxLength(128);
            b.Property(x => x.CreatedAtUtc).HasColumnType("datetime(6)");

            b.HasIndex(x => x.TargetUserId);
        });

        modelBuilder.Entity<VirtueRoleTierConfig>(b =>
        {
            b.HasKey(x => new { x.GuildId, x.TierIndex });

            b.Property(x => x.GuildId).HasColumnType("bigint unsigned");
            b.Property(x => x.RoleId).HasColumnType("bigint unsigned");
            b.Property(x => x.MinVirtue).HasColumnType("int");
            b.Property(x => x.MaxVirtue).HasColumnType("int");

            b.HasIndex(x => x.GuildId);
        });
    }
}
