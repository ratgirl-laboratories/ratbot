using RatBot.Domain.Primitives;
using RatBot.Infrastructure.Converters;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RatBot.Infrastructure.Configurations;

public sealed class MetaSuggestionSettingsConfiguration : IEntityTypeConfiguration<MetaSuggestionSettings>
{
    public void Configure(EntityTypeBuilder<MetaSuggestionSettings> builder)
    {
        builder.ToTable("MetaSuggestionSettings");
        builder.HasKey(x => x.GuildId);

        builder.Property(x => x.GuildId).HasColumnType("bigint").HasConversion<SnowflakeValueConverter<GuildSnowflake>>().ValueGeneratedNever();
        builder.Property(x => x.SuggestForumChannelId).HasColumnType("bigint").HasConversion<SnowflakeValueConverter<ChannelSnowflake>>();
    }
}
