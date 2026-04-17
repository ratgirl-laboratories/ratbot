using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RatBot.Infrastructure.Persistence.Configurations;

public sealed class EmojiUsageCountConfiguration : IEntityTypeConfiguration<EmojiUsageCount>
{
    public void Configure(EntityTypeBuilder<EmojiUsageCount> builder)
    {
        builder.ToTable("EmojiUsageCounts");
        builder.HasKey(x => x.EmojiId);
        builder.Property(x => x.EmojiId).HasMaxLength(128);
        builder.Property(x => x.UsageCount).HasColumnType("int");
    }
}