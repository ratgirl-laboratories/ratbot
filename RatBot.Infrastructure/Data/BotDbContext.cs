using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RatBot.Domain.Entities;

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
    /// Gets the quorum scope configuration set.
    /// </summary>
    public DbSet<QuorumScopeConfig> QuorumScopeConfigs => Set<QuorumScopeConfig>();

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
        modelBuilder.Entity<QuorumScopeConfig>(b =>
        {
            b.HasKey(x => new
            {
                x.GuildId,
                x.ScopeType,
                x.ScopeId,
            });

            b.Property(x => x.GuildId).HasColumnType("numeric(20,0)");
            b.Property(x => x.ScopeId).HasColumnType("numeric(20,0)");
            b.Property(x => x.RoleIds)
                .HasColumnType("jsonb")
                .HasConversion(
                    roleIds => JsonSerializer.Serialize(roleIds, (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<List<ulong>>(value, (JsonSerializerOptions?)null) ?? new List<ulong>()
                )
                .Metadata.SetValueComparer(
                    new ValueComparer<IReadOnlyList<ulong>>(
                        (left, right) => left!.SequenceEqual(right!),
                        roleIds => roleIds.Aggregate(0, (hash, roleId) => HashCode.Combine(hash, roleId)),
                        roleIds => roleIds.ToList()
                    )
                );
            b.Property(x => x.ScopeType).HasConversion<int>();
            b.Property(x => x.QuorumProportion).HasPrecision(6, 4);

            b.HasIndex(x => x.GuildId);
            b.HasIndex(x => new { x.GuildId, x.ScopeType });
        });

        modelBuilder.Entity<EmojiUsageCount>(b =>
        {
            b.HasKey(x => x.EmojiId);

            b.Property(x => x.EmojiId).HasMaxLength(128);
            b.Property(x => x.UsageCount).HasColumnType("int");
        });
    }
}
