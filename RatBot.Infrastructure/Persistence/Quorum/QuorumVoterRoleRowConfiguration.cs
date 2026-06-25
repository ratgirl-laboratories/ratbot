using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RatBot.Infrastructure.Persistence.Quorum;

internal sealed class QuorumVoterRoleRowConfiguration : IEntityTypeConfiguration<QuorumVoterRoleRow>
{
    public void Configure(EntityTypeBuilder<QuorumVoterRoleRow> builder)
    {
        builder.ToTable("quorum_voter_roles");

        builder.HasKey(role => new { role.QuorumConfigurationId, role.RoleId });

        builder.Property(role => role.QuorumConfigurationId).HasColumnName("quorum_configuration_id").ValueGeneratedNever();

        builder.Property(role => role.RoleId).HasColumnName("role_id").HasColumnType("bigint").HasConversion<long>();
    }
}