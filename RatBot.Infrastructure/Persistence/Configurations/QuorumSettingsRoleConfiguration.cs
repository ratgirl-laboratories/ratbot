using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RatBot.Infrastructure.Persistence.Configurations;

public sealed class QuorumSettingsRoleConfiguration : IEntityTypeConfiguration<QuorumSettingsRole>
{
    public void Configure(EntityTypeBuilder<QuorumSettingsRole> builder)
    {
        builder.ToTable("QuorumConfigRoles");

        builder.Property(x => x.GuildId).HasColumnType("bigint").HasConversion<long>();
        builder.Property(x => x.TargetType).HasColumnType("integer");
        builder.Property(x => x.TargetId).HasColumnType("bigint").HasConversion<long>();

        builder.Property(x => x.Id).HasColumnName("RoleId").HasColumnType("bigint").HasConversion<long>();

        builder.HasKey(x => new
        {
            x.GuildId,
            x.TargetType,
            x.TargetId,
            x.Id
        });
    }
}