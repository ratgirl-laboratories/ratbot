using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RatBot.Infrastructure.Features.Timezone.Persistence;

public sealed class UserTimezoneRowConfiguration : IEntityTypeConfiguration<UserTimezoneRow>
{
    public void Configure(EntityTypeBuilder<UserTimezoneRow> builder)
    {
        builder.ToTable(
            "user_timezones",
            table => table.HasCheckConstraint("CK_user_timezones_timezone_id_not_empty", "length(btrim(timezone_id)) > 0")
        );

        builder.HasKey(x => x.UserId);

        builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("bigint").ValueGeneratedNever();
        builder.Property(x => x.TimezoneId).HasColumnName("timezone_id").HasColumnType("character varying(128)").HasMaxLength(128).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone").IsRequired();
    }
}
