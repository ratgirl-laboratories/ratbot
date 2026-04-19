using ErrorOr;
using NSubstitute;
using RatBot.Application.Quorum;
using RatBot.Domain.Quorum;
using Serilog;
using Shouldly;

namespace RatBot.Application.Tests.Quorum;

[TestFixture]
public sealed class QuorumSettingsWriterTests
{
    private IQuorumSettingsRepository _repository = null!;
    private QuorumSettingsWriter _writer = null!;
    private QuorumTarget _target;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IQuorumSettingsRepository>();

        ILogger logger = Substitute.For<ILogger>();
        logger.ForContext<QuorumSettingsWriter>().Returns(logger);

        _writer = new QuorumSettingsWriter(_repository, logger);
        _target = QuorumTarget.Create(123, QuorumSettingsType.Channel, 456).Value;
    }

    [Test]
    public async Task UpsertAsync_WithValidInput_DeduplicatesRolesAndPersistsSettings()
    {
        // Arrange
        _repository
            .GetAsync(_target)
            .Returns(Task.FromResult<ErrorOr<QuorumSettings>>(Error.NotFound(description: "not found")));

        _repository.UpsertAsync(Arg.Any<QuorumSettings>()).Returns(Task.FromResult<ErrorOr<Success>>(Result.Success));

        // Act
        ErrorOr<QuorumSettingsUpsertResult> result = await _writer.UpsertAsync(_target, [10, 20, 10], 0.75);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Created.ShouldBeTrue();
        result.Value.Config.Roles.Select(role => role.Id).ShouldBe([10UL, 20UL]);
        result.Value.Config.Proportion.ShouldBe(0.75);

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
    public async Task UpsertAsync_WithEmptyRoles_ReturnsValidationErrorAndDoesNotWrite()
    {
        // Arrange
        _repository
            .GetAsync(_target)
            .Returns(Task.FromResult<ErrorOr<QuorumSettings>>(Error.NotFound(description: "not found")));

        // Act
        ErrorOr<QuorumSettingsUpsertResult> result = await _writer.UpsertAsync(_target, [], 0.75);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);
        result.FirstError.Description.ShouldBe("At least one role must be provided.");

        await _repository.Received(1).GetAsync(_target);
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
        ErrorOr<QuorumSettingsUpsertResult> result = await _writer.UpsertAsync(_target, [10], quorumProportion);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Type.ShouldBe(ErrorType.Validation);

        await _repository.DidNotReceiveWithAnyArgs().GetAsync(default);
        await _repository.DidNotReceiveWithAnyArgs().UpsertAsync(null!);
    }

    [Test]
    public async Task UpsertAsync_WithExistingSettings_UpdatesExistingAggregate()
    {
        // Arrange
        QuorumSettings existing = CreateSettings(_target, [10], 0.5);

        _repository.GetAsync(_target).Returns(Task.FromResult<ErrorOr<QuorumSettings>>(existing));
        _repository.UpsertAsync(Arg.Any<QuorumSettings>()).Returns(Task.FromResult<ErrorOr<Success>>(Result.Success));

        // Act
        ErrorOr<QuorumSettingsUpsertResult> result = await _writer.UpsertAsync(_target, [20, 20, 30], 0.75);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Created.ShouldBeFalse();
        result.Value.Config.ShouldBeSameAs(existing);
        existing.Proportion.ShouldBe(0.75);
        existing.Roles.Select(role => role.Id).ShouldBe([20UL, 30UL]);

        await _repository.Received(1).UpsertAsync(existing);
    }

    [Test]
    public async Task UpsertAsync_WithRepositoryReadError_ReturnsErrorAndDoesNotWrite()
    {
        // Arrange
        Error repositoryError = Error.Failure(description: "database unavailable");
        _repository.GetAsync(_target).Returns(Task.FromResult<ErrorOr<QuorumSettings>>(repositoryError));

        // Act
        ErrorOr<QuorumSettingsUpsertResult> result = await _writer.UpsertAsync(_target, [10], 0.75);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(repositoryError);
        await _repository.DidNotReceiveWithAnyArgs().UpsertAsync(null!);
    }

    [Test]
    public async Task UpsertAsync_WithRepositoryWriteError_ReturnsError()
    {
        // Arrange
        Error repositoryError = Error.Failure(description: "database unavailable");

        _repository
            .GetAsync(_target)
            .Returns(Task.FromResult<ErrorOr<QuorumSettings>>(Error.NotFound(description: "not found")));

        _repository.UpsertAsync(Arg.Any<QuorumSettings>())
            .Returns(Task.FromResult<ErrorOr<Success>>(repositoryError));

        // Act
        ErrorOr<QuorumSettingsUpsertResult> result = await _writer.UpsertAsync(_target, [10], 0.75);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(repositoryError);
    }

    [Test]
    public async Task DeleteAsync_ReturnsRepositoryResult()
    {
        // Arrange
        _repository.DeleteAsync(_target).Returns(Task.FromResult<ErrorOr<Deleted>>(Result.Deleted));

        // Act
        ErrorOr<Deleted> result = await _writer.DeleteAsync(_target);

        // Assert
        result.IsError.ShouldBeFalse();
        await _repository.Received(1).DeleteAsync(_target);
    }

    [Test]
    public async Task DeleteAsync_WithRepositoryError_ReturnsError()
    {
        // Arrange
        Error repositoryError = Error.NotFound(description: "not found");
        _repository.DeleteAsync(_target).Returns(Task.FromResult<ErrorOr<Deleted>>(repositoryError));

        // Act
        ErrorOr<Deleted> result = await _writer.DeleteAsync(_target);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(repositoryError);
        await _repository.Received(1).DeleteAsync(_target);
    }

    private static QuorumSettings CreateSettings(
        QuorumTarget target,
        IEnumerable<ulong> roleIds,
        double quorumProportion) =>
        QuorumSettings.Create(target, roleIds, Proportion.Create(quorumProportion).Value).Value;
}
