using ErrorOr;
using NSubstitute;
using RatBot.Application.Quorum;
using RatBot.Domain.Quorum;
using Serilog;
using Shouldly;

namespace RatBot.Application.Tests.Quorum;

[TestFixture]
public sealed class QuorumSettingsServiceTests
{
    private IQuorumSettingsRepository _repository = null!;
    private QuorumSettingsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IQuorumSettingsRepository>();

        ILogger logger = Substitute.For<ILogger>();
        logger.ForContext<QuorumSettingsService>().Returns(logger);

        _service = new QuorumSettingsService(_repository, logger);
    }

    [Test]
    public async Task UpsertAsync_WithValidInput_DeduplicatesRolesAndPersistsSettings()
    {
        // Arrange
        _repository
            .GetAsync(123, QuorumSettingsType.Channel, 456)
            .Returns(Task.FromResult<ErrorOr<QuorumSettings>>(Error.NotFound(description: "not found")));

        _repository.UpsertAsync(Arg.Any<QuorumSettings>()).Returns(Task.FromResult<ErrorOr<Success>>(Result.Success));

        // Act
        ErrorOr<QuorumSettingsUpsertResult> result = await _service.UpsertAsync(123, QuorumSettingsType.Channel, 456, [10, 20, 10], 0.75);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Created.ShouldBeTrue();
        result.Value.Config.Roles.Select(role => role.Id).ShouldBe([10UL, 20UL]);
        result.Value.Config.QuorumProportion.ShouldBe(0.75);

        await _repository
            .Received(1)
            .UpsertAsync(
                Arg.Is<QuorumSettings>(settings =>
                    settings.GuildId == 123
                    && settings.TargetType == QuorumSettingsType.Channel
                    && settings.TargetId == 456
                    && settings.Roles.Select(role => role.Id).SequenceEqual(new List<ulong> { 10UL, 20UL })
                )
            );
    }

    [Test]
    public async Task UpsertAsync_WithEmptyRoles_ReturnsValidationErrorAndDoesNotReadOrWrite()
    {
        // Arrange

        // Act
        ErrorOr<QuorumSettingsUpsertResult> result = await _service.UpsertAsync(123, QuorumSettingsType.Channel, 456, [], 0.75);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Description.ShouldBe("At least one role must be provided.");

        await _repository.DidNotReceiveWithAnyArgs().GetAsync(0, default, 0);
        await _repository.DidNotReceiveWithAnyArgs().UpsertAsync(null!);
    }

    [Test]
    [TestCase(0)]
    [TestCase(-0.0001)]
    [TestCase(1.0001)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public async Task UpsertAsync_WithInvalidProportion_ReturnsValidationError(double quorumProportion)
    {
        // Arrange

        // Act
        ErrorOr<QuorumSettingsUpsertResult> result = await _service.UpsertAsync(123, QuorumSettingsType.Channel, 456, [10], quorumProportion);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);

        await _repository.DidNotReceiveWithAnyArgs().GetAsync(0, default, 0);
        await _repository.DidNotReceiveWithAnyArgs().UpsertAsync(null!);
    }
}
