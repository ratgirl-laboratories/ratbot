using ErrorOr;
using NSubstitute;
using RatBot.Application.Common;
using RatBot.Application.Administration;
using Shouldly;

namespace RatBot.Application.Tests.Administration;

[TestFixture]
public sealed class AdminSendServiceTests
{

    private AdminSendService _service = null!;
    private IMessageChannelWriter _channelWriter = null!;
    [SetUp]
    public void SetUp()
    {
        _service = new AdminSendService();
        _channelWriter = Substitute.For<IMessageChannelWriter>();
    }

    [Test]
    public async Task SendAsync_WhenChannelIsMissing_ReturnsChannelNotFoundAndSendsNothing()
    {
        // Arrange
        _channelWriter.GetChannelAsync(123).Returns(AdminSendErrors.ChannelNotFound);

        // Act
        ErrorOr<string> result = await _service.SendAsync(_channelWriter, 123, "hello");

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(AdminSendErrors.ChannelNotFound);
        await _channelWriter.DidNotReceiveWithAnyArgs().ValidateBotCanSendAsync(default);
        await _channelWriter.DidNotReceiveWithAnyArgs().SendMessagesAsync(default, default!);
    }

    [Test]
    public async Task SendAsync_WhenBotLacksPermission_ReturnsInsufficientPermissionsAndSendsNothing()
    {
        // Arrange
        _channelWriter.GetChannelAsync(123).Returns(new ResolvedMessageChannel(123, "<#123>"));
        _channelWriter.ValidateBotCanSendAsync(123).Returns(AdminSendErrors.InsufficientPermissions);

        // Act
        ErrorOr<string> result = await _service.SendAsync(_channelWriter, 123, "hello");

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(AdminSendErrors.InsufficientPermissions);
        await _channelWriter.DidNotReceiveWithAnyArgs().SendMessagesAsync(default, default!);
    }

    [Test]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\n\t")]
    public async Task SendAsync_WhenMessageIsEmpty_ReturnsEmptyMessageAndDoesNotResolveChannel(string message)
    {
        // Arrange

        // Act
        ErrorOr<string> result = await _service.SendAsync(_channelWriter, 123, message);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(AdminSendErrors.EmptyMessage);
        await _channelWriter.DidNotReceiveWithAnyArgs().GetChannelAsync(default);
        await _channelWriter.DidNotReceiveWithAnyArgs().ValidateBotCanSendAsync(default);
        await _channelWriter.DidNotReceiveWithAnyArgs().SendMessagesAsync(default, default!);
    }

    [Test]
    public async Task SendAsync_WithSingleChunk_SendsMessageAndReturnsSuccessText()
    {
        // Arrange
        _channelWriter.GetChannelAsync(123).Returns(new ResolvedMessageChannel(123, "<#123>"));
        _channelWriter.ValidateBotCanSendAsync(123).Returns(Result.Success);
        _channelWriter.SendMessagesAsync(123, Arg.Any<IReadOnlyList<string>>()).Returns(1);

        // Act
        ErrorOr<string> result = await _service.SendAsync(_channelWriter, 123, "hello");

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe("Sent your message to <#123>.");

        await _channelWriter
            .Received(1)
            .SendMessagesAsync(
                123,
                Arg.Is<IReadOnlyList<string>>(messages => messages.Count == 1 && messages[0] == "hello"));
    }

    [Test]
    public async Task SendAsync_WithMultipleChunks_SendsChunksInOrderAndReturnsPartCount()
    {
        // Arrange
        _channelWriter.GetChannelAsync(123).Returns(new ResolvedMessageChannel(123, "<#123>"));
        _channelWriter.ValidateBotCanSendAsync(123).Returns(Result.Success);
        _channelWriter.SendMessagesAsync(123, Arg.Any<IReadOnlyList<string>>()).Returns(2);
        string message = $"{new string('a', 1999)}\nsecond";

        // Act
        ErrorOr<string> result = await _service.SendAsync(_channelWriter, 123, message);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe("Sent your message to <#123> in 2 parts.");

        await _channelWriter
            .Received(1)
            .SendMessagesAsync(
                123,
                Arg.Is<IReadOnlyList<string>>(messages =>
                    messages.Count == 2 && messages[0] == new string('a', 1999) + "\n" && messages[1] == "second"));
    }

    [Test]
    public async Task SendAsync_WhenMessageSplitFails_ReturnsSplitErrorAndSendsNothing()
    {
        // Arrange
        Error splitError = Error.Validation("AdminSend.SplitFailed", "split failed");
        _service = new AdminSendService(_ => splitError);
        _channelWriter.GetChannelAsync(123).Returns(new ResolvedMessageChannel(123, "<#123>"));
        _channelWriter.ValidateBotCanSendAsync(123).Returns(Result.Success);

        // Act
        ErrorOr<string> result = await _service.SendAsync(_channelWriter, 123, "hello");

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(splitError);
        await _channelWriter.DidNotReceiveWithAnyArgs().SendMessagesAsync(default, default!);
    }

    [Test]
    public async Task SendAsync_WhenSendFails_SurfacesException()
    {
        // Arrange
        _channelWriter.GetChannelAsync(123).Returns(new ResolvedMessageChannel(123, "<#123>"));
        _channelWriter.ValidateBotCanSendAsync(123).Returns(Result.Success);

        _channelWriter
            .SendMessagesAsync(123, Arg.Any<IReadOnlyList<string>>())
            .Returns(Task.FromException<ErrorOr<int>>(new InvalidOperationException("send failed")));

        // Act & Assert
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            _service.SendAsync(_channelWriter, 123, "hello"));

        exception.Message.ShouldBe("send failed");
    }
}
