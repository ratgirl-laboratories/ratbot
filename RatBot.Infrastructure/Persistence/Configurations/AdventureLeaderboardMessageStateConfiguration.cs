namespace RatBot.Infrastructure.Persistence.Configurations;

public sealed class AdventureLeaderboardMessageStateConfiguration : IEntityTypeConfiguration<AdventureLeaderboardMessageState>
{
    public void Configure(EntityTypeBuilder<AdventureLeaderboardMessageState> builder)
    {
        builder.ToTable("AdventureLeaderboardMessageState");

        builder.HasKey(x => new { x.GuildId, x.Id });

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.GuildId).IsRequired().HasConversion<long>().HasColumnType("bigint").ValueGeneratedNever();
        builder.Property(x => x.ChannelId).IsRequired().HasConversion<long>().HasColumnType("bigint");
        builder.Property(x => x.MessageId).IsRequired().HasConversion<long>().HasColumnType("bigint");
        builder.Property(x => x.Year).IsRequired();
        builder.Property(x => x.LastRenderHash).IsRequired();

        builder.HasIndex(x => new { x.GuildId, x.Id }).IsUnique();
    }
}
