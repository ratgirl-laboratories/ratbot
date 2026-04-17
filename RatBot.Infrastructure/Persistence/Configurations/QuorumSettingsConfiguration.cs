using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RatBot.Infrastructure.Persistence.Configurations;

public sealed class QuorumSettingsConfiguration : IEntityTypeConfiguration<QuorumSettings>
{
    public void Configure(EntityTypeBuilder<QuorumSettings> builder)
    {
        builder.ToTable("QuorumConfigs");
        builder.HasKey(x => new { x.GuildId, x.TargetType, x.TargetId });

        builder.Property(x => x.GuildId).HasColumnType("bigint").HasConversion<long>();
        builder.Property(x => x.TargetType).HasColumnType("integer");
        builder.Property(x => x.TargetId).HasColumnType("bigint").HasConversion<long>();
        builder.Property(x => x.QuorumProportion).HasColumnType("double precision").HasPrecision(6, 4);

        builder.HasIndex(x => x.GuildId);
        builder.HasIndex(x => new { x.GuildId, x.TargetType });
        builder.Ignore(x => x.RoleIds);
    }
}