using RatBot.Infrastructure.Converters;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RatBot.Domain.Primitives;

namespace RatBot.Infrastructure.Configurations;

public sealed class QuorumSettingsRoleConfiguration : IEntityTypeConfiguration<RoleSnowflake>
{
    public void Configure(EntityTypeBuilder<RoleSnowflake> builder)
    {
        builder.ToTable("QuorumConfigRoles");

        // Shadow properties for composite key from parent
        builder.Property<GuildSnowflake>("GuildId").HasColumnType("bigint").HasConversion<SnowflakeValueConverter<GuildSnowflake>>();
        builder.Property<QuorumSettingsType>("TargetType").HasColumnType("integer");
        builder.Property<ulong>("TargetId").HasColumnType("bigint").HasConversion<long>();

        builder.Property(x => x.Id).HasColumnType("bigint").HasConversion<SnowflakeValueConverter<RoleSnowflake>>();

        builder.HasKey("GuildId", "TargetType", "TargetId", "Id");

        builder
            .HasOne<QuorumSettings>()
            .WithMany()
            .HasForeignKey("GuildId", "TargetType", "TargetId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
