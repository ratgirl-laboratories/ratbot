using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RatBot.Infrastructure.Settings;
using RatBot.Infrastructure.Settings.Meta;

namespace RatBot.Infrastructure.Configurations;

public sealed class MetaSuggestionSettingsConfiguration : IEntityTypeConfiguration<MetaSuggestionSettingsEntity>
{
    public void Configure(EntityTypeBuilder<MetaSuggestionSettingsEntity> builder)
    {
        builder.ToTable("MetaSuggestionSettings");
        builder.HasKey(x => x.GuildId);

        builder.Property(x => x.GuildId).HasColumnType("bigint").HasConversion<long>().ValueGeneratedNever();
        builder.Property(x => x.SuggestForumChannelId).HasColumnType("bigint").HasConversion<long>();
    }
}
