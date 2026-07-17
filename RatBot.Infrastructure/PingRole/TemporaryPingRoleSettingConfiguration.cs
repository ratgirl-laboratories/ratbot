using RatBot.Domain.SecretRole;

namespace RatBot.Infrastructure.PingRole;

public sealed class TemporaryPingRoleSettingConfiguration : IEntityTypeConfiguration<SecretRoleSetting>
{
    public void Configure(EntityTypeBuilder<SecretRoleSetting> builder)
    {
        builder.ToTable("TemporaryPingRoleSettings");

        builder.HasKey(x => x.GuildId);

        builder.Property(x => x.GuildId).IsRequired().HasConversion<long>().HasColumnType("bigint").ValueGeneratedNever();
        builder.Property(x => x.RoleId).IsRequired().HasConversion<long>().HasColumnType("bigint");
    }
}
