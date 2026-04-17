using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RatBot.Application.Administration;
using RatBot.Discord.Commands.Admin;

namespace RatBot.Discord.Tests.Commands.Admin;

[TestFixture]
public sealed class AdminSendInteractionTests
{
    private const ulong InvokerUserId = 123;
    private const ulong ChannelId = 456;

    [Test]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task SendAsync_WhenBotLacksRequiredChannelPermission_RespondsEphemeralWithInsufficientPermissions(
        bool canView,
        bool canSendMessages)
    {
        // Arrange
        AdminSendInteractionContextBuilder builder = new AdminSendInteractionContextBuilder()
            .WithBotChannelPermissions(canView, canSendMessages);

        // Act
        await builder.ExecuteAdminSendAsync();

        // Assert
        await builder
            .Interaction.Received(1)
            .RespondAsync(AdminSendErrors.InsufficientPermissions.Description, ephemeral: true);

        await builder.Interaction.DidNotReceive().RespondWithModalAsync(Arg.Any<Modal>(), Arg.Any<RequestOptions>());
    }

    [Test]
    public async Task SendAsync_WhenBotHasRequiredChannelPermissions_RespondsWithAdminSendModal()
    {
        // Arrange
        AdminSendInteractionContextBuilder builder = new AdminSendInteractionContextBuilder()
            .WithBotChannelPermissions(true, true);

        // Act
        await builder.ExecuteAdminSendAsync();

        // Assert
        await builder
            .Interaction.Received(1)
            .RespondWithModalAsync(
                Arg.Is<Modal>(modal => modal.CustomId == $"admin-send:{InvokerUserId}:{ChannelId}"),
                Arg.Any<RequestOptions>());

        await builder.Interaction.DidNotReceive().RespondAsync(Arg.Any<string>(), ephemeral: true);
    }

    [Test]
    public async Task SendModalAsync_WhenInvokerDoesNotMatchContextUser_RespondsEphemeralAndDoesNotSend()
    {
        // Arrange
        AdminSendInteractionContextBuilder builder = new AdminSendInteractionContextBuilder();

        // Act
        await builder.ExecuteAdminSendModalAsync(InvokerUserId + 1, "hello");

        // Assert
        await builder
            .Interaction.Received(1)
            .RespondAsync("Only the user who opened this modal can submit it.", ephemeral: true);

        await builder.Interaction.DidNotReceive().DeferAsync(true);
        await builder.Guild.DidNotReceive().GetTextChannelAsync(Arg.Any<ulong>());
        await builder.TargetChannel.DidNotReceive().SendMessageAsync(Arg.Any<string>());
    }

    [Test]
    public async Task SendModalAsync_WhenInvokerMatchesAndSendSucceeds_DefersThenFollowsUpEphemeralSuccess()
    {
        // Arrange
        AdminSendInteractionContextBuilder builder = new AdminSendInteractionContextBuilder()
            .WithInteractionHasResponded(true);

        // Act
        await builder.ExecuteAdminSendModalAsync(InvokerUserId, "hello");

        // Assert
        await builder.Interaction.Received(1).DeferAsync(true);
        await builder.TargetChannel.Received(1).SendMessageAsync("hello");

        await builder
            .Interaction.Received(1)
            .FollowupAsync($"Sent your message to {builder.TargetChannel.Mention}.", ephemeral: true);

        await builder.Interaction.DidNotReceive().RespondAsync(Arg.Any<string>(), ephemeral: true);
    }

    [Test]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public async Task SendModalAsync_WhenSendFlowFails_ReturnsErrorDescriptionEphemeral(
        bool channelExists,
        bool canSendMessages)
    {
        // Arrange
        AdminSendInteractionContextBuilder builder = new AdminSendInteractionContextBuilder()
            .WithInteractionHasResponded(true)
            .WithTargetChannelExists(channelExists)
            .WithBotChannelPermissions(canSendMessages, canSendMessages);

        string expectedError = channelExists
            ? AdminSendErrors.InsufficientPermissions.Description
            : AdminSendErrors.ChannelNotFound.Description;

        // Act
        await builder.ExecuteAdminSendModalAsync(InvokerUserId, "hello");

        // Assert
        await builder.Interaction.Received(1).DeferAsync(true);

        await builder
            .Interaction.Received(1)
            .FollowupAsync(expectedError, ephemeral: true);

        await builder.TargetChannel.DidNotReceive().SendMessageAsync(Arg.Any<string>());
    }

    private sealed class AdminSendInteractionContextBuilder
    {
        private readonly IGuildUser _botUser = Substitute.For<IGuildUser>();
        private readonly IInteractionContext _context = Substitute.For<IInteractionContext>();

        private readonly ChannelPermissions _defaultBotPermissions =
            new ChannelPermissions(viewChannel: true, sendMessages: true);

        private readonly IGuildUser _invokerUser = Substitute.For<IGuildUser>();
        private readonly IMessageChannel _sourceChannel = Substitute.For<IMessageChannel>();

        public AdminSendInteractionContextBuilder()
        {
            TargetChannel.Id.Returns(ChannelId);
            TargetChannel.Mention.Returns($"<#{ChannelId}>");
            TargetChannel.SendMessageAsync(Arg.Any<string>()).Returns(Substitute.For<IUserMessage>());
            _invokerUser.Id.Returns(InvokerUserId);
            _invokerUser.GuildPermissions.Returns(new GuildPermissions(administrator: true));
            Guild.GetCurrentUserAsync().Returns(_botUser);
            Guild.GetTextChannelAsync(ChannelId).Returns(TargetChannel);
            _botUser.GetPermissions(TargetChannel).Returns(_defaultBotPermissions);

            Interaction.User.Returns(_invokerUser);
            Interaction.HasResponded.Returns(false);

            _context.Client.Returns(Substitute.For<IDiscordClient>());
            _context.Guild.Returns(Guild);
            _context.Channel.Returns(_sourceChannel);
            _context.User.Returns(_invokerUser);
            _context.Interaction.Returns(Interaction);
        }

        public IDiscordInteraction Interaction { get; } = Substitute.For<IDiscordInteraction>();

        public IGuild Guild { get; } = Substitute.For<IGuild>();

        public ITextChannel TargetChannel { get; } = Substitute.For<ITextChannel>();

        public AdminSendInteractionContextBuilder WithInteractionHasResponded(bool hasResponded)
        {
            Interaction.HasResponded.Returns(hasResponded);

            return this;
        }

        public AdminSendInteractionContextBuilder WithTargetChannelExists(bool exists)
        {
            Guild
                .GetTextChannelAsync(ChannelId)
                .Returns(
                    exists
                        ? TargetChannel
                        : null);

            return this;
        }

        public AdminSendInteractionContextBuilder WithBotChannelPermissions(bool canView, bool canSendMessages)
        {
            ChannelPermissions expectedPermissions = new ChannelPermissions(
                viewChannel: canView,
                sendMessages: canSendMessages);

            _botUser
                .GetPermissions(TargetChannel)
                .Returns(expectedPermissions);

            return this;
        }

        public async Task ExecuteAdminSendAsync()
        {
            await using ServiceProvider services = new ServiceCollection()
                .AddScoped<AdminSendService>()
                .BuildServiceProvider();

            InteractionService interactionService = new InteractionService(new DiscordSocketClient());
            await interactionService.AddModuleAsync<AdminModule>(services);

            AdminModule module = new AdminModule(services.GetRequiredService<AdminSendService>());
            ((IInteractionModuleBase)module).SetContext(_context);

            await module.SendAsync(TargetChannel);
        }

        public async Task ExecuteAdminSendModalAsync(ulong invokerUserId, string message)
        {
            await using ServiceProvider services = new ServiceCollection()
                .AddScoped<AdminSendService>()
                .BuildServiceProvider();

            InteractionService interactionService = new InteractionService(new DiscordSocketClient());
            await interactionService.AddModuleAsync<AdminModule>(services);

            AdminModule module = new AdminModule(services.GetRequiredService<AdminSendService>());
            ((IInteractionModuleBase)module).SetContext(_context);

            await module.SendModalAsync(invokerUserId, ChannelId, new AdminSendModal { Message = message });
        }
    }
}
