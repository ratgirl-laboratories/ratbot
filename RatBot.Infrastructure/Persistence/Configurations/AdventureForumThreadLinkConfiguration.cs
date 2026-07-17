namespace RatBot.Infrastructure.Persistence.Configurations;

public sealed class AdventureForumThreadLinkConfiguration : IEntityTypeConfiguration<AdventureForumThreadLink>
{
    public void Configure(EntityTypeBuilder<AdventureForumThreadLink> builder)
    {
        builder.ToTable(
            "AdventureForumThreadLinks",
            table =>
            {
                table.HasCheckConstraint("CK_AdventureForumThreadLinks_ScorePartIndex", "\"ScorePartIndex\" >= 1 AND \"ScorePartIndex\" <= 20");
            }
        );

        builder.HasKey(x => new { x.GuildId, x.ScorePartIndex });

        builder.Property(x => x.GuildId).IsRequired().HasConversion<long>().HasColumnType("bigint").ValueGeneratedNever();
        builder.Property(x => x.ScorePartIndex).ValueGeneratedNever();
        builder.Property(x => x.ThreadId).IsRequired().HasConversion<long>().HasColumnType("bigint");

        builder.HasIndex(x => new { x.GuildId, x.ScorePartIndex }).IsUnique();
        builder.HasIndex(x => new { x.GuildId, x.ThreadId }).IsUnique();
    }
}
