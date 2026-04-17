using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RatBot.Infrastructure.Persistence.Configurations;

public sealed class MetaSuggestionSettingsConfiguration : IEntityTypeConfiguration<MetaSuggestionSettings>
{
    public void Configure(EntityTypeBuilder<MetaSuggestionSettings> builder)
    {
        builder.ToTable("MetaSuggestionSettings");
        builder.HasKey(x => x.GuildId);

        builder.Property(x => x.GuildId).HasColumnType("bigint").HasConversion<long>().ValueGeneratedNever();
        builder.Property(x => x.SuggestForumChannelId).HasColumnType("bigint").HasConversion<long>();
    }
}