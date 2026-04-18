using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RatBot.Infrastructure.Persistence.Configurations;

public sealed class QuorumSettingsConfiguration : IEntityTypeConfiguration<QuorumSettings>
{
    public void Configure(EntityTypeBuilder<QuorumSettings> builder)
    {
        builder.ToTable("QuorumConfigs");

        builder.HasKey(x => new
        {
            x.GuildId,
            x.TargetType,
            x.TargetId
        });

        builder.Property(x => x.GuildId).HasColumnType("bigint").HasConversion<long>();
        builder.Property(x => x.TargetType).HasColumnType("integer");
        builder.Property(x => x.TargetId).HasColumnType("bigint").HasConversion<long>();
        builder.Property(x => x.QuorumProportion).HasColumnType("double precision").HasPrecision(6, 4);

        builder
            .HasMany(x => x.Roles)
            .WithOne()
            .HasForeignKey(x => new
            {
                x.GuildId,
                x.TargetType,
                x.TargetId
            })
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.GuildId);
        builder.HasIndex(x => new { x.GuildId, x.TargetType });
    }
}