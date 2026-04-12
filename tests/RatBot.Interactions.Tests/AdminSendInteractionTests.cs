using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RatBot.Application.Features.AdminSend;
using RatBot.Domain.Features.AdminSend;
using RatBot.Interactions.Modules.Admin;

namespace RatBot.Interactions.Tests;

public sealed class AdminSendInteractionTests
{
    private const ulong InvokerUserId = 123;
    private const ulong ChannelId = 456;

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task SendAsync_WhenBotLacksRequiredChannelPermission_RespondsEphemeralWithInsufficientPermissions(
        bool canView,
        bool canSendMessages)
    {
        AdminSendInteractionContextBuilder builder = new AdminSendInteractionContextBuilder()
            .WithBotChannelPermissions(canView, canSendMessages);

        await builder.ExecuteAdminSendAsync();

        await builder
            .Interaction.Received(1)
            .RespondAsync(AdminSendErrors.InsufficientPermissions.Description, ephemeral: true);

        await builder.Interaction.DidNotReceive().RespondWithModalAsync(Arg.Any<Modal>(), Arg.Any<RequestOptions>());
    }

    [Fact]
    public async Task SendAsync_WhenBotHasRequiredChannelPermissions_RespondsWithAdminSendModal()
    {
        AdminSendInteractionContextBuilder builder = new AdminSendInteractionContextBuilder()
            .WithBotChannelPermissions(canView: true, canSendMessages: true);

        await builder.ExecuteAdminSendAsync();

        await builder
            .Interaction.Received(1)
            .RespondWithModalAsync(
                Arg.Is<Modal>(modal => modal.CustomId == $"admin-send:{InvokerUserId}:{ChannelId}"),
                Arg.Any<RequestOptions>());

        await builder.Interaction.DidNotReceive().RespondAsync(Arg.Any<string>(), ephemeral: true);
    }

    [Fact]
    public async Task SendModalAsync_WhenInvokerDoesNotMatchContextUser_RespondsEphemeralAndDoesNotSend()
    {
        AdminSendInteractionContextBuilder builder = new AdminSendInteractionContextBuilder();

        await builder.ExecuteAdminSendModalAsync(invokerUserId: InvokerUserId + 1, message: "hello");

        await builder
            .Interaction.Received(1)
            .RespondAsync("Only the user who opened this modal can submit it.", ephemeral: true);

        await builder.Interaction.DidNotReceive().DeferAsync(ephemeral: true);
        await builder.Guild.DidNotReceive().GetTextChannelAsync(Arg.Any<ulong>());
        await builder.TargetChannel.DidNotReceive().SendMessageAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task SendModalAsync_WhenInvokerMatchesAndSendSucceeds_DefersThenFollowsUpEphemeralSuccess()
    {
        AdminSendInteractionContextBuilder builder = new AdminSendInteractionContextBuilder()
            .WithInteractionHasResponded(true);

        await builder.ExecuteAdminSendModalAsync(invokerUserId: InvokerUserId, message: "hello");

        await builder.Interaction.Received(1).DeferAsync(ephemeral: true);
        await builder.TargetChannel.Received(1).SendMessageAsync("hello");
        await builder
            .Interaction.Received(1)
            .FollowupAsync($"Sent your message to {builder.TargetChannel.Mention}.", ephemeral: true);

        await builder.Interaction.DidNotReceive().RespondAsync(Arg.Any<string>(), ephemeral: true);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task SendModalAsync_WhenSendFlowFails_ReturnsErrorDescriptionEphemeral(
        bool channelExists,
        bool canSendMessages)
    {
        AdminSendInteractionContextBuilder builder = new AdminSendInteractionContextBuilder()
            .WithInteractionHasResponded(true)
            .WithTargetChannelExists(channelExists)
            .WithBotChannelPermissions(canView: canSendMessages, canSendMessages);

        string expectedError = channelExists
            ? AdminSendErrors.InsufficientPermissions.Description
            : AdminSendErrors.ChannelNotFound.Description;

        await builder.ExecuteAdminSendModalAsync(invokerUserId: InvokerUserId, message: "hello");

        await builder.Interaction.Received(1).DeferAsync(ephemeral: true);
        await builder
            .Interaction.Received(1)
            .FollowupAsync(expectedError, ephemeral: true);

        await builder.TargetChannel.DidNotReceive().SendMessageAsync(Arg.Any<string>());
    }

    private sealed class AdminSendInteractionContextBuilder
    {
        private readonly IGuild _guild = Substitute.For<IGuild>();
        private readonly IGuildUser _botUser = Substitute.For<IGuildUser>();
        private readonly IGuildUser _invokerUser = Substitute.For<IGuildUser>();
        private readonly ITextChannel _targetChannel = Substitute.For<ITextChannel>();
        private readonly IMessageChannel _sourceChannel = Substitute.For<IMessageChannel>();
        private readonly IInteractionContext _context = Substitute.For<IInteractionContext>();

        private readonly ChannelPermissions _defaultBotPermissions =
            new ChannelPermissions(viewChannel: true, sendMessages: true);

        public AdminSendInteractionContextBuilder()
        {
            _targetChannel.Id.Returns(ChannelId);
            _targetChannel.Mention.Returns($"<#{ChannelId}>");
            _targetChannel.SendMessageAsync(Arg.Any<string>()).Returns(Substitute.For<IUserMessage>());
            _invokerUser.Id.Returns(InvokerUserId);
            _invokerUser.GuildPermissions.Returns(new GuildPermissions(administrator: true));
            _guild.GetCurrentUserAsync().Returns(_botUser);
            _guild.GetTextChannelAsync(ChannelId).Returns(_targetChannel);
            _botUser.GetPermissions(_targetChannel).Returns(_defaultBotPermissions);

            Interaction.User.Returns(_invokerUser);
            Interaction.HasResponded.Returns(false);

            _context.Client.Returns(Substitute.For<IDiscordClient>());
            _context.Guild.Returns(_guild);
            _context.Channel.Returns(_sourceChannel);
            _context.User.Returns(_invokerUser);
            _context.Interaction.Returns(Interaction);
        }

        public IDiscordInteraction Interaction { get; } = Substitute.For<IDiscordInteraction>();

        public IGuild Guild => _guild;

        public ITextChannel TargetChannel => _targetChannel;

        public AdminSendInteractionContextBuilder WithInteractionHasResponded(bool hasResponded)
        {
            Interaction.HasResponded.Returns(hasResponded);

            return this;
        }

        public AdminSendInteractionContextBuilder WithTargetChannelExists(bool exists)
        {
            _guild
                .GetTextChannelAsync(ChannelId)
                .Returns(exists ? _targetChannel : null);

            return this;
        }

        public AdminSendInteractionContextBuilder WithBotChannelPermissions(bool canView, bool canSendMessages)
        {
            ChannelPermissions expectedPermissions = new ChannelPermissions(
                viewChannel: canView,
                sendMessages: canSendMessages);

            _botUser
                .GetPermissions(_targetChannel)
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

            await module.SendAsync(_targetChannel);
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
