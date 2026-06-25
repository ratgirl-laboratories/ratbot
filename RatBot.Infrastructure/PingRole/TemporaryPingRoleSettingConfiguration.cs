using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RatBot.Domain.SecretRole;

namespace RatBot.Infrastructure.PingRole;

public sealed class TemporaryPingRoleSettingConfiguration : IEntityTypeConfiguration<SecretRoleSetting>
{
    public void Configure(EntityTypeBuilder<SecretRoleSetting> builder)
    {
        builder.ToTable("TemporaryPingRoleSettings", table => table.HasCheckConstraint("CK_TemporaryPingRoleSettings_SingletonId", "\"Id\" = 1"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.GuildId).IsRequired();
        builder.Property(x => x.RoleId).IsRequired();
    }
}
