using ErrorOr;
using NSubstitute;
using RatBot.Application.Common.Discord;
using RatBot.Application.Features.AdminSend;
using RatBot.Domain.Features.AdminSend;
using Shouldly;

namespace RatBot.Application.Tests;

public sealed class AdminSendServiceTests
{
    private readonly AdminSendService _service = new AdminSendService();

    [Fact]
    public async Task SendAsync_WhenChannelIsMissing_ReturnsChannelNotFoundAndSendsNothing()
    {
        IDiscordChannelService channelService = Substitute.For<IDiscordChannelService>();
        channelService.GetTextChannelAsync(123).Returns(AdminSendErrors.ChannelNotFound);

        ErrorOr<string> result = await _service.SendAsync(channelService, 123, "hello");

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(AdminSendErrors.ChannelNotFound);
        await channelService.DidNotReceiveWithAnyArgs().ValidateBotCanSendAsync(default);
        await channelService.DidNotReceiveWithAnyArgs().SendMessagesAsync(default, default!);
    }

    [Fact]
    public async Task SendAsync_WhenBotLacksPermission_ReturnsInsufficientPermissionsAndSendsNothing()
    {
        IDiscordChannelService channelService = Substitute.For<IDiscordChannelService>();
        channelService.GetTextChannelAsync(123).Returns(new ResolvedTextChannel(123, "<#123>"));
        channelService.ValidateBotCanSendAsync(123).Returns(AdminSendErrors.InsufficientPermissions);

        ErrorOr<string> result = await _service.SendAsync(channelService, 123, "hello");

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(AdminSendErrors.InsufficientPermissions);
        await channelService.DidNotReceiveWithAnyArgs().SendMessagesAsync(default, default!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t")]
    public async Task SendAsync_WhenMessageIsEmpty_ReturnsEmptyMessageAndDoesNotResolveChannel(string message)
    {
        IDiscordChannelService channelService = Substitute.For<IDiscordChannelService>();

        ErrorOr<string> result = await _service.SendAsync(channelService, 123, message);

        result.IsError.ShouldBeTrue();
        result.FirstError.ShouldBe(AdminSendErrors.EmptyMessage);
        await channelService.DidNotReceiveWithAnyArgs().GetTextChannelAsync(default);
        await channelService.DidNotReceiveWithAnyArgs().ValidateBotCanSendAsync(default);
        await channelService.DidNotReceiveWithAnyArgs().SendMessagesAsync(default, default!);
    }

    [Fact]
    public async Task SendAsync_WithSingleChunk_SendsMessageAndReturnsSuccessText()
    {
        IDiscordChannelService channelService = Substitute.For<IDiscordChannelService>();
        channelService.GetTextChannelAsync(123).Returns(new ResolvedTextChannel(123, "<#123>"));
        channelService.ValidateBotCanSendAsync(123).Returns(Result.Success);
        channelService.SendMessagesAsync(123, Arg.Any<IReadOnlyList<string>>()).Returns(1);

        ErrorOr<string> result = await _service.SendAsync(channelService, 123, "hello");

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe("Sent your message to <#123>.");
        await channelService.Received(1).SendMessagesAsync(
            123,
            Arg.Is<IReadOnlyList<string>>(messages => messages.Count == 1 && messages[0] == "hello"));
    }

    [Fact]
    public async Task SendAsync_WithMultipleChunks_SendsChunksInOrderAndReturnsPartCount()
    {
        IDiscordChannelService channelService = Substitute.For<IDiscordChannelService>();
        channelService.GetTextChannelAsync(123).Returns(new ResolvedTextChannel(123, "<#123>"));
        channelService.ValidateBotCanSendAsync(123).Returns(Result.Success);
        channelService.SendMessagesAsync(123, Arg.Any<IReadOnlyList<string>>()).Returns(2);
        string message = $"{new string('a', 1999)}\nsecond";

        ErrorOr<string> result = await _service.SendAsync(channelService, 123, message);

        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe("Sent your message to <#123> in 2 parts.");
        await channelService.Received(1).SendMessagesAsync(
            123,
            Arg.Is<IReadOnlyList<string>>(messages =>
                messages.Count == 2 && messages[0] == new string('a', 1999) + "\n" && messages[1] == "second"));
    }

    [Fact]
    public async Task SendAsync_WhenSendFails_SurfacesException()
    {
        IDiscordChannelService channelService = Substitute.For<IDiscordChannelService>();
        channelService.GetTextChannelAsync(123).Returns(new ResolvedTextChannel(123, "<#123>"));
        channelService.ValidateBotCanSendAsync(123).Returns(Result.Success);
        channelService.SendMessagesAsync(123, Arg.Any<IReadOnlyList<string>>())
            .Returns(Task.FromException<ErrorOr<int>>(new InvalidOperationException("send failed")));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            _service.SendAsync(channelService, 123, "hello"));

        exception.Message.ShouldBe("send failed");
    }
}
