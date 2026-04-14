using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RatBot.Infrastructure.Settings;
using RatBot.Infrastructure.Settings.Meta;

namespace RatBot.Infrastructure.Configurations;

public sealed class MetaSuggestionConfiguration : IEntityTypeConfiguration<MetaSuggestion>
{
    public void Configure(EntityTypeBuilder<MetaSuggestion> builder)
    {
        builder.ToTable("MetaSuggestions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.GuildId).HasColumnType("bigint").HasConversion<long>();
        builder.Property(x => x.AuthorUserId).HasColumnType("bigint").HasConversion<long>();
        builder.Property(x => x.SubmittedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.Title).HasMaxLength(75);
        builder.Property(x => x.Summary).HasMaxLength(1500);
        builder.Property(x => x.Motivation).HasMaxLength(1950);
        builder.Property(x => x.Specification).HasMaxLength(1950);

        builder.Property(x => x.Anonymity).HasColumnType("integer");
        builder.Property(x => x.State).HasColumnType("integer");

        builder.Property(x => x.ForumChannelId).HasColumnType("bigint").HasConversion<long>();
        builder.Property(x => x.ThreadChannelId).HasColumnType("bigint").HasConversion<long?>();

        builder.HasIndex(x => x.GuildId);
        builder.HasIndex(x => new { x.GuildId, x.State });
        builder.HasIndex(x => x.ThreadChannelId).IsUnique();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_MetaSuggestions_Title_NotBlank", """length(btrim("Title")) > 0""");
            table.HasCheckConstraint("CK_MetaSuggestions_Summary_NotBlank", """length(btrim("Summary")) > 0""");
            table.HasCheckConstraint("CK_MetaSuggestions_Motivation_NotBlank", """length(btrim("Motivation")) > 0""");
            table.HasCheckConstraint("CK_MetaSuggestions_Specification_NotBlank", """length(btrim("Specification")) > 0""");
        });
    }
}
