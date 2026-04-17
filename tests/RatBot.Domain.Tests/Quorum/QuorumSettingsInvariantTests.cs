using System.Text.Json;
using RatBot.Domain.Meta;
using RatBot.Domain.Quorum;
using Shouldly;

namespace RatBot.Domain.Tests.Quorum;

/// <summary>
///     Tests for the QuorumConfig domain model invariants.
/// </summary>
[TestFixture]
public sealed class QuorumSettingsInvariantTests
{
    private static ulong[] RoleIds(params ulong[] ids) => [..ids];

    [Test]
    public void Create_WithValidParameters_ReturnsInstance()
    {
        // Arrange

        // Act
        QuorumSettings config = QuorumSettings.Create(
            123,
            QuorumSettingsType.Channel,
            456,
            RoleIds(10UL, 20UL, 10UL, 30UL),
            0.75);

        // Assert
        config.GuildId.ShouldBe(123UL);
        config.TargetType.ShouldBe(QuorumSettingsType.Channel);
        config.TargetId.ShouldBe(456UL);
        config.RoleIds.ShouldBe(RoleIds(10UL, 20UL, 30UL));
        config.QuorumProportion.ShouldBe(0.75);
    }

    [Test]
    public void Create_SingleRoleOverload_SetsRoleAndProportion()
    {
        // Arrange

        // Act
        QuorumSettings config = QuorumSettings.Create(123, QuorumSettingsType.Category, 456, 99, 0.6);

        // Assert
        config.RoleIds.ShouldBe(RoleIds(99UL));
        config.QuorumProportion.ShouldBe(0.6);
    }

    [Test]
    public void Create_WithZeroGuildId_ThrowsArgumentOutOfRange()
    {
        // Arrange

        // Act
        ArgumentOutOfRangeException ex = Should.Throw<ArgumentOutOfRangeException>(() =>
            QuorumSettings.Create(0, QuorumSettingsType.Channel, 1, RoleIds(1UL), 0.5));

        // Assert
        ex.ParamName.ShouldBe("value");
    }

    [Test]
    public void Create_WithZeroTargetId_ThrowsArgumentOutOfRange()
    {
        // Arrange

        // Act
        ArgumentOutOfRangeException ex = Should.Throw<ArgumentOutOfRangeException>(() =>
            QuorumSettings.Create(1, QuorumSettingsType.Channel, 0, RoleIds(1UL), 0.5));

        // Assert
        ex.ParamName.ShouldBe("targetId");
    }

    [Test]
    public void Create_WithInvalidTargetType_ThrowsArgumentOutOfRange()
    {
        // Arrange

        // Act
        ArgumentOutOfRangeException ex = Should.Throw<ArgumentOutOfRangeException>(() =>
            QuorumSettings.Create(1, (QuorumSettingsType)999, 2, RoleIds(1UL), 0.5));

        // Assert
        ex.ParamName.ShouldBe("targetType");
    }

    [Test]
    public void Create_WithDefaultRoleIds_ThrowsArgumentOutOfRange()
    {
        // Arrange
        ulong[] roleIds = [];

        // Act
        ArgumentOutOfRangeException ex = Should.Throw<ArgumentOutOfRangeException>(() =>
            QuorumSettings.Create(1, QuorumSettingsType.Channel, 2, roleIds, 0.5));

        // Assert
        ex.ParamName.ShouldBe("roleIds");
    }

    [Test]
    public void Create_WithEmptyRoleIds_ThrowsArgumentOutOfRange()
    {
        // Arrange

        // Act
        ArgumentOutOfRangeException ex = Should.Throw<ArgumentOutOfRangeException>(() =>
            QuorumSettings.Create(1, QuorumSettingsType.Channel, 2, RoleIds(), 0.5));

        // Assert
        ex.ParamName.ShouldBe("roleIds");
    }

    [Test]
    public void Create_WithZeroRoleId_ThrowsArgumentOutOfRange()
    {
        // Arrange

        // Act
        ArgumentOutOfRangeException ex = Should.Throw<ArgumentOutOfRangeException>(() =>
            QuorumSettings.Create(1, QuorumSettingsType.Channel, 2, RoleIds(5UL, 0UL), 0.5));

        // Assert
        ex.ParamName.ShouldBe("roleIds");
    }

    [Test]
    [TestCase(0)]
    [TestCase(-0.0001)]
    [TestCase(1.0001)]
    public void Create_WithOutOfRangeProportion_ThrowsArgumentOutOfRange(double quorumProportion)
    {
        // Arrange

        // Act
        ArgumentOutOfRangeException ex = Should.Throw<ArgumentOutOfRangeException>(() =>
            QuorumSettings.Create(1, QuorumSettingsType.Channel, 2, RoleIds(10UL), quorumProportion));

        // Assert
        ex.ParamName.ShouldBe("value");
    }

    [Test]
    public void Create_WithNaNProportion_ThrowsArgumentOutOfRange()
    {
        // Arrange

        // Act
        ArgumentOutOfRangeException ex = Should.Throw<ArgumentOutOfRangeException>(() =>
            QuorumSettings.Create(1, QuorumSettingsType.Channel, 2, RoleIds(10UL), double.NaN));

        // Assert
        ex.ParamName.ShouldBe("value");
    }

    [Test]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void Create_WithInfiniteProportion_ThrowsArgumentOutOfRange(double quorumProportion)
    {
        // Arrange

        // Act
        ArgumentOutOfRangeException ex = Should.Throw<ArgumentOutOfRangeException>(() =>
            QuorumSettings.Create(1, QuorumSettingsType.Channel, 2, RoleIds(10UL), quorumProportion));

        // Assert
        ex.ParamName.ShouldBe("value");
    }

    [Test]
    public void Reconfigure_WithValidInputs_UpdatesRolesAndProportion()
    {
        // Arrange
        QuorumSettings config = QuorumSettings.Create(1, QuorumSettingsType.Channel, 2, RoleIds(11UL), 0.5);

        // Act
        QuorumSettings reconfigured = config.Reconfigure(RoleIds(3UL, 2UL, 2UL, 1UL), 0.9);

        // Assert
        reconfigured.GuildId.ShouldBe(1UL);
        reconfigured.TargetType.ShouldBe(QuorumSettingsType.Channel);
        reconfigured.TargetId.ShouldBe(2UL);
        reconfigured.RoleIds.ShouldBe(RoleIds(3UL, 2UL, 1UL));
        reconfigured.QuorumProportion.ShouldBe(0.9);
    }

    [Test]
    public void Reconfigure_WithSingleRoleOverload_UpdatesRolesAndProportion()
    {
        // Arrange
        QuorumSettings config = QuorumSettings.Create(1, QuorumSettingsType.Channel, 2, RoleIds(11UL), 0.5);

        // Act
        QuorumSettings reconfigured = config.Reconfigure(42UL, 1.0);

        // Assert
        reconfigured.GuildId.ShouldBe(1UL);
        reconfigured.TargetType.ShouldBe(QuorumSettingsType.Channel);
        reconfigured.TargetId.ShouldBe(2UL);
        reconfigured.RoleIds.ShouldBe(RoleIds(42UL));
        reconfigured.QuorumProportion.ShouldBe(1.0);
    }

    [Test]
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(1.01)]
    public void Reconfigure_WithOutOfRangeProportion_ThrowsArgumentOutOfRange(double quorumProportion)
    {
        // Arrange
        QuorumSettings config = QuorumSettings.Create(1, QuorumSettingsType.Channel, 2, RoleIds(10UL), 0.5);

        // Act
        ArgumentOutOfRangeException ex =
            Should.Throw<ArgumentOutOfRangeException>(() => _ = config.Reconfigure(RoleIds(10UL), quorumProportion));

        // Assert
        ex.ParamName.ShouldBe("value");
    }

    [Test]
    public void Reconfigure_WithEmptyRoles_ThrowsArgumentOutOfRange()
    {
        // Arrange
        QuorumSettings config = QuorumSettings.Create(1, QuorumSettingsType.Channel, 2, RoleIds(10UL), 0.5);

        // Act
        ArgumentOutOfRangeException ex =
            Should.Throw<ArgumentOutOfRangeException>(() => _ = config.Reconfigure(RoleIds(), 0.5));

        // Assert
        ex.ParamName.ShouldBe("roleIds");
    }

    [Test]
    public void JsonRoundTrip_WithPersistedShape_DeserializesSuccessfully()
    {
        // Arrange
        QuorumSettings original = QuorumSettings.Create(
            123,
            QuorumSettingsType.Channel,
            456,
            RoleIds(10UL, 20UL, 30UL),
            0.75);

        // Act
        string json = JsonSerializer.Serialize(original);

        QuorumSettings deserialized = JsonSerializer.Deserialize<QuorumSettings>(json)
                                      ?? throw new InvalidOperationException(
                                          "Expected JSON payload to deserialize into QuorumSettings.");

        // Assert
        deserialized.GuildId.ShouldBe(original.GuildId);
        deserialized.TargetType.ShouldBe(original.TargetType);
        deserialized.TargetId.ShouldBe(original.TargetId);
        deserialized.RoleIds.ShouldBe(original.RoleIds);
        deserialized.QuorumProportion.ShouldBe(original.QuorumProportion);
    }
}
