using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RatBot.Infrastructure.Persistence.Quorum;

internal sealed class QuorumConfigurationRowConfiguration : IEntityTypeConfiguration<QuorumConfigurationRow>
{
    public void Configure(EntityTypeBuilder<QuorumConfigurationRow> builder)
    {
        builder.ToTable(
            "quorum_configurations",
            table => table.HasCheckConstraint("ck_quorum_configurations_proportion", "proportion > 0 and proportion <= 1")
        );

        builder.HasKey(configuration => configuration.Id);

        builder.Property(configuration => configuration.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(configuration => configuration.GuildId).HasColumnName("guild_id").HasColumnType("bigint").HasConversion<long>();

        builder.Property(configuration => configuration.ChannelId).HasColumnName("channel_id").HasColumnType("bigint").HasConversion<long>();

        builder.Property(configuration => configuration.ChannelKind).HasColumnName("channel_kind").HasMaxLength(16).IsRequired();

        builder.Property(configuration => configuration.Proportion).HasColumnName("proportion").HasPrecision(9, 8).IsRequired();

        builder.HasIndex(configuration => new { configuration.GuildId, configuration.ChannelId }).IsUnique();

        builder
            .HasMany(configuration => configuration.VoterRoles)
            .WithOne()
            .HasForeignKey(role => role.QuorumConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}