using ErrorOr;
using RatBot.Domain.Quorum;
using Shouldly;

namespace RatBot.Domain.Tests.Quorum;

[TestFixture]
public sealed class QuorumSettingsTests
{
    [Test]
    public void Create_WithValidInput_CreatesSettingsWithDistinctRoleRows()
    {
        // Arrange
        QuorumTarget target = QuorumTarget.Create(123, QuorumSettingsType.Channel, 456).Value;
        Proportion proportion = Proportion.Create(0.75).Value;

        // Act
        ErrorOr<QuorumSettings> result = QuorumSettings.Create(target, [10, 20, 10, 30], proportion);

        // Assert
        result.IsError.ShouldBeFalse();
        QuorumSettings settings = result.Value;
        settings.GuildId.ShouldBe(123UL);
        settings.TargetType.ShouldBe(QuorumSettingsType.Channel);
        settings.TargetId.ShouldBe(456UL);
        settings.Roles.Select(role => role.Id).ShouldBe([10UL, 20UL, 30UL]);
        settings.Proportion.ShouldBe(0.75);
        settings.Roles.Select(role => role.GuildId).ShouldBe([123UL, 123UL, 123UL]);

        settings.Roles.Select(role => role.TargetType).ShouldBe([QuorumSettingsType.Channel, QuorumSettingsType.Channel, QuorumSettingsType.Channel]);

        settings.Roles.Select(role => role.TargetId).ShouldBe([456UL, 456UL, 456UL]);
    }

    [Test]
    public void Create_WithNoRoles_ReturnsValidationError()
    {
        // Arrange
        QuorumTarget target = QuorumTarget.Create(123, QuorumSettingsType.Channel, 456).Value;
        Proportion proportion = Proportion.Create(0.75).Value;

        // Act
        ErrorOr<QuorumSettings> result = QuorumSettings.Create(target, [], proportion);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Description.ShouldBe("At least one role must be provided.");
    }

    [Test]
    public void Create_WithDefaultProportion_ReturnsValidationError()
    {
        // Arrange
        QuorumTarget target = QuorumTarget.Create(123, QuorumSettingsType.Channel, 456).Value;

        // Act
        ErrorOr<QuorumSettings> result = QuorumSettings.Create(target, [10], default);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
    }

    [Test]
    public void Create_WithDefaultTarget_ReturnsValidationError()
    {
        // Arrange
        Proportion proportion = Proportion.Create(0.75).Value;

        // Act
        ErrorOr<QuorumSettings> result = QuorumSettings.Create(default, [10], proportion);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Description.ShouldBe("Invalid quorum configuration type.");
    }

    [Test]
    public void Update_WithNoRoles_ReturnsValidationErrorAndLeavesExistingSettingsUnchanged()
    {
        // Arrange
        QuorumTarget target = QuorumTarget.Create(123, QuorumSettingsType.Channel, 456).Value;
        Proportion originalProportion = Proportion.Create(0.75).Value;
        QuorumSettings settings = QuorumSettings.Create(target, [10, 20], originalProportion).Value;
        Proportion updatedProportion = Proportion.Create(0.5).Value;

        // Act
        ErrorOr<Success> result = settings.Update([], updatedProportion);

        // Assert
        result.IsError.ShouldBeTrue();
        settings.Proportion.ShouldBe(0.75);
        settings.Roles.Select(role => role.Id).ShouldBe([10UL, 20UL]);
    }

    [Test]
    public void Update_WithDefaultProportion_ReturnsValidationErrorAndLeavesExistingSettingsUnchanged()
    {
        // Arrange
        QuorumTarget target = QuorumTarget.Create(123, QuorumSettingsType.Channel, 456).Value;
        Proportion originalProportion = Proportion.Create(0.75).Value;
        QuorumSettings settings = QuorumSettings.Create(target, [10, 20], originalProportion).Value;

        // Act
        ErrorOr<Success> result = settings.Update([30], default);

        // Assert
        result.IsError.ShouldBeTrue();
        settings.Proportion.ShouldBe(0.75);
        settings.Roles.Select(role => role.Id).ShouldBe([10UL, 20UL]);
    }

    [Test]
    [TestCase(0)]
    [TestCase(-0.0001)]
    [TestCase(1.0001)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void QuorumProportion_Create_WithInvalidValue_ReturnsValidationError(double value)
    {
        // Act
        ErrorOr<Proportion> result = Proportion.Create(value);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
    }

    [Test]
    public void QuorumTarget_Create_WithInvalidTargetType_ReturnsValidationError()
    {
        // Act
        ErrorOr<QuorumTarget> result = QuorumTarget.Create(123, (QuorumSettingsType)999, 456);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Description.ShouldBe("Invalid quorum configuration type.");
    }
}
