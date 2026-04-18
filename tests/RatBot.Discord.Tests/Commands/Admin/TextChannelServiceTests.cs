using Discord;
using ErrorOr;
using NSubstitute;
using RatBot.Application.Administration;
using RatBot.Discord.Commands.Admin;
using Shouldly;

namespace RatBot.Discord.Tests.Commands.Admin;

[TestFixture]
public sealed class TextChannelServiceTests
{
    private const ulong ChannelId = 456;

    private readonly ChannelPermissions _canSendPermissions = new ChannelPermissions(viewChannel: true, sendMessages: true);

    private IGuild _guild = null!;
    private IGuildUser _botUser = null!;
    private ITextChannel _channel = null!;
    private TextChannelService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _guild = Substitute.For<IGuild>();
        _botUser = Substitute.For<IGuildUser>();
        _channel = Substitute.For<ITextChannel>();
        _service = new TextChannelService(_guild);

        _guild.GetCurrentUserAsync().Returns(_botUser);
        _guild.GetTextChannelAsync(ChannelId).Returns(_channel);
        _botUser.GetPermissions(_channel).Returns(_canSendPermissions);
        _channel.SendMessageAsync(Arg.Any<string>()).Returns(Substitute.For<IUserMessage>());
    }

    [Test]
    public async Task ValidateBotCanSendAsync_WhenBotHasRequiredPermissions_ReturnsSuccess()
    {
        // Act
        ErrorOr<Success> result = await _service.ValidateBotCanSendAsync(ChannelId);

        // Assert
        result.IsError.ShouldBeFalse();
    }

    [Test]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task ValidateBotCanSendAsync_WhenBotLacksRequiredPermission_ReturnsInsufficientPermissions(bool canView, bool canSendMessages)
    {
        // Arrange
        _botUser.GetPermissions(_channel).Returns(new ChannelPermissions(viewChannel: canView, sendMessages: canSendMessages));

        // Act
        ErrorOr<Success> result = await _service.ValidateBotCanSendAsync(ChannelId);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(AdminSendErrors.InsufficientPermissions);
    }

    [Test]
    public async Task ValidateBotCanSendAsync_WhenChannelIsMissing_ReturnsChannelNotFound()
    {
        // Arrange
        _guild.GetTextChannelAsync(ChannelId).Returns((ITextChannel?)null);

        // Act
        ErrorOr<Success> result = await _service.ValidateBotCanSendAsync(ChannelId);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(AdminSendErrors.ChannelNotFound);
        await _guild.DidNotReceive().GetCurrentUserAsync();
    }

    [Test]
    public async Task SendMessagesAsync_WhenChannelExists_SendsEachMessageAndReturnsCount()
    {
        // Arrange
        List<string> sentMessages = [];
        _channel.SendMessageAsync(Arg.Do<string>(sentMessages.Add)).Returns(Substitute.For<IUserMessage>());

        // Act
        ErrorOr<int> result = await _service.SendMessagesAsync(ChannelId, ["first", "second"]);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe(2);
        sentMessages.ShouldBe(["first", "second"]);
    }

    [Test]
    public async Task SendMessagesAsync_WhenChannelIsMissing_ReturnsChannelNotFoundAndSendsNothing()
    {
        // Arrange
        _guild.GetTextChannelAsync(ChannelId).Returns((ITextChannel?)null);

        // Act
        ErrorOr<int> result = await _service.SendMessagesAsync(ChannelId, ["hello"]);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(AdminSendErrors.ChannelNotFound);
        await _channel.DidNotReceive().SendMessageAsync(Arg.Any<string>());
    }
}
