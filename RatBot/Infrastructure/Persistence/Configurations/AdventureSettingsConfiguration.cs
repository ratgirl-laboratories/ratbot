using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RatBot.Infrastructure.Persistence.Configurations;

public sealed class AdventureSettingsConfiguration : IEntityTypeConfiguration<AdventureSettings>
{
    public void Configure(EntityTypeBuilder<AdventureSettings> builder)
    {
        builder.ToTable("AdventureSettings");

        builder.HasKey(x => x.GuildId);

        builder.Property(x => x.GuildId).IsRequired().HasConversion<long>().HasColumnType("bigint").ValueGeneratedNever();
        builder.Property(x => x.AdventurerRoleId).IsRequired().HasConversion<long>().HasColumnType("bigint");
    }
}
