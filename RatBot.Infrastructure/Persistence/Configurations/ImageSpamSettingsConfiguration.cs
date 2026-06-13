using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RatBot.Infrastructure.Persistence.Configurations;

public sealed class ImageSpamSettingsConfiguration : IEntityTypeConfiguration<ImageSpamSettings>
{
    public void Configure(EntityTypeBuilder<ImageSpamSettings> builder)
    {
        builder.ToTable(
            "ImageSpamSettings",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ImageSpamSettings_RequiredChannelCount_Positive",
                    "\"RequiredChannelCount\" > 0");

                table.HasCheckConstraint(
                    "CK_ImageSpamSettings_RequiredAttachmentCount_Positive",
                    "\"RequiredAttachmentCount\" > 0");

                table.HasCheckConstraint(
                    "CK_ImageSpamSettings_BurstDurationSeconds_Positive",
                    "\"BurstDurationSeconds\" > 0");
            });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.RequiredChannelCount).IsRequired();
        builder.Property(x => x.RequiredAttachmentCount).IsRequired();
        builder.Property(x => x.BurstDurationSeconds).IsRequired();
    }
}