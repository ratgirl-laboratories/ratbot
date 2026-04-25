using ErrorOr;
using NSubstitute;
using RatBot.Application.Quorum;
using RatBot.Domain.Quorum;
using Shouldly;

namespace RatBot.Application.Tests.Quorum;

[TestFixture]
public sealed class QuorumSettingsReaderTests
{
    private IQuorumSettingsRepository _repository = null!;
    private QuorumSettingsReader _reader = null!;
    private QuorumTarget _channelTarget;
    private QuorumTarget _categoryTarget;

    private static QuorumSettings CreateSettings(
        QuorumTarget target,
        IEnumerable<ulong> roleIds,
        double quorumProportion) =>
        QuorumSettings.Create(target, roleIds, Proportion.Create(quorumProportion).Value).Value;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IQuorumSettingsRepository>();
        _reader = new QuorumSettingsReader(_repository);

        _channelTarget = QuorumTarget.Create(123, QuorumSettingsType.Channel, 456).Value;
        _categoryTarget = QuorumTarget.Create(123, QuorumSettingsType.Category, 789).Value;
    }

    [Test]
    public async Task GetAsync_ReturnsRepositoryResult()
    {
        // Arrange
        QuorumSettings settings = CreateSettings(_channelTarget, [10], 0.75);
        _repository.GetAsync(_channelTarget).Returns(Task.FromResult<ErrorOr<QuorumSettings>>(settings));

        // Act
        ErrorOr<QuorumSettings> result = await _reader.GetAsync(_channelTarget);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBeSameAs(settings);
        await _repository.Received(1).GetAsync(_channelTarget);
    }

    [Test]
    public async Task GetEffectiveAsync_WhenChannelSettingsExist_ReturnsChannelSettingsAndDoesNotReadCategory()
    {
        // Arrange
        QuorumSettings channelSettings = CreateSettings(_channelTarget, [10], 0.75);
        _repository.GetAsync(_channelTarget).Returns(Task.FromResult<ErrorOr<QuorumSettings>>(channelSettings));

        // Act
        ErrorOr<QuorumSettings> result = await _reader.GetEffectiveAsync(123, 456, 789);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBeSameAs(channelSettings);
        await _repository.Received(1).GetAsync(_channelTarget);
        await _repository.DidNotReceive().GetAsync(_categoryTarget);
    }

    [Test]
    public async Task GetEffectiveAsync_WhenChannelIsMissingAndCategoryExists_ReturnsCategorySettings()
    {
        // Arrange
        QuorumSettings categorySettings = CreateSettings(_categoryTarget, [20], 0.5);

        _repository
            .GetAsync(_channelTarget)
            .Returns(Task.FromResult<ErrorOr<QuorumSettings>>(Error.NotFound(description: "channel not found")));

        _repository.GetAsync(_categoryTarget).Returns(Task.FromResult<ErrorOr<QuorumSettings>>(categorySettings));

        // Act
        ErrorOr<QuorumSettings> result = await _reader.GetEffectiveAsync(123, 456, 789);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBeSameAs(categorySettings);
        await _repository.Received(1).GetAsync(_channelTarget);
        await _repository.Received(1).GetAsync(_categoryTarget);
    }

    [Test]
    public async Task GetEffectiveAsync_WhenChannelIsMissingAndNoCategoryIsProvided_ReturnsChannelError()
    {
        // Arrange
        Error channelError = Error.NotFound(description: "channel not found");
        _repository.GetAsync(_channelTarget).Returns(Task.FromResult<ErrorOr<QuorumSettings>>(channelError));

        // Act
        ErrorOr<QuorumSettings> result = await _reader.GetEffectiveAsync(123, 456, null);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(channelError);
        await _repository.Received(1).GetAsync(_channelTarget);
        await _repository.DidNotReceive().GetAsync(_categoryTarget);
    }

    [Test]
    public async Task GetEffectiveAsync_WhenChannelReadFails_ReturnsErrorAndDoesNotReadCategory()
    {
        // Arrange
        Error channelError = Error.Failure(description: "database unavailable");
        _repository.GetAsync(_channelTarget).Returns(Task.FromResult<ErrorOr<QuorumSettings>>(channelError));

        // Act
        ErrorOr<QuorumSettings> result = await _reader.GetEffectiveAsync(123, 456, 789);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(channelError);
        await _repository.Received(1).GetAsync(_channelTarget);
        await _repository.DidNotReceive().GetAsync(_categoryTarget);
    }
}