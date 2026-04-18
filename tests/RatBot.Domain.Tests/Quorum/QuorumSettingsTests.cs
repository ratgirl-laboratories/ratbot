using RatBot.Domain.Quorum;
using Shouldly;

namespace RatBot.Domain.Tests.Quorum;

[TestFixture]
public sealed class QuorumSettingsTests
{
    [Test]
    public void ReplaceRoles_ReplacesExistingRolesWithDistinctRoleRows()
    {
        // Arrange
        QuorumSettings settings = new QuorumSettings(123, QuorumSettingsType.Channel, 456, 0.75);

        // Act
        settings.ReplaceRoles([10, 20, 10, 30]);

        // Assert
        settings.GuildId.ShouldBe(123UL);
        settings.TargetType.ShouldBe(QuorumSettingsType.Channel);
        settings.TargetId.ShouldBe(456UL);
        settings.Roles.Select(role => role.Id).ShouldBe([10UL, 20UL, 30UL]);
        settings.QuorumProportion.ShouldBe(0.75);
        settings.Roles.Select(role => role.GuildId).ShouldBe([123UL, 123UL, 123UL]);

        settings.Roles.Select(role => role.TargetType).ShouldBe([QuorumSettingsType.Channel, QuorumSettingsType.Channel, QuorumSettingsType.Channel]);

        settings.Roles.Select(role => role.TargetId).ShouldBe([456UL, 456UL, 456UL]);
    }
}
