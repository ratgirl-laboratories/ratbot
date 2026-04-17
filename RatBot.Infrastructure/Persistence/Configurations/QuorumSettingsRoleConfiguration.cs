using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RatBot.Infrastructure.Persistence.Models;

namespace RatBot.Infrastructure.Persistence.Configurations;

public sealed class QuorumSettingsRoleConfiguration : IEntityTypeConfiguration<QuorumSettingsRole>
{
    public void Configure(EntityTypeBuilder<QuorumSettingsRole> builder)
    {
        builder.ToTable("QuorumConfigRoles");

        builder.Property<ulong>("GuildId").HasColumnType("bigint").HasConversion<long>();
        builder.Property<QuorumSettingsType>("TargetType").HasColumnType("integer");
        builder.Property<ulong>("TargetId").HasColumnType("bigint").HasConversion<long>();

        builder.Property(x => x.Id).HasColumnName("RoleId").HasColumnType("bigint").HasConversion<long>();

        builder.HasKey("GuildId", "TargetType", "TargetId", "Id");

        builder
            .HasOne<QuorumSettings>()
            .WithMany()
            .HasForeignKey("GuildId", "TargetType", "TargetId")
            .OnDelete(DeleteBehavior.Cascade);
    }
}