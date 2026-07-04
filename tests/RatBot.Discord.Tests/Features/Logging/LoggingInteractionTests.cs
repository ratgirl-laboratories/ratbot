using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using RatBot.Discord.Features.Logging.Commands;
using RatBot.Infrastructure.Data;
using Shouldly;

namespace RatBot.Discord.Tests.Features.Logging;

[TestFixture]
public sealed class LoggingInteractionTests
{
    private const ulong ChannelId = 200;
    private const ulong DeleteLogChannelId = 300;
    private const ulong EditLogChannelId = 400;
    private const ulong GuildId = 100;

    [Test]
    public async Task ExcludeAsync_WhenChannelOmitted_UsesCurrentChannel()
    {
        await using BotDbContext db = CreateDbContext(nameof(ExcludeAsync_WhenChannelOmitted_UsesCurrentChannel));
        LoggingInteractionFixture fixture = new LoggingInteractionFixture(db);

        await fixture.Module.ExcludeAsync();

        (await db.LoggingExcludedChannels.SingleAsync()).ChannelId.ShouldBe(ChannelId);
        await fixture.Interaction.Received(1).RespondAsync($"Logging is now excluded in <#{ChannelId}>.", ephemeral: true);
    }

    [Test]
    public async Task IncludeAsync_RemovesExclusion()
    {
        await using BotDbContext db = CreateDbContext(nameof(IncludeAsync_RemovesExclusion));
        LoggingInteractionFixture fixture = new LoggingInteractionFixture(db);
        await fixture.Module.ExcludeAsync();
        fixture.Interaction.ClearReceivedCalls();

        await fixture.Module.IncludeAsync();

        (await db.LoggingExcludedChannels.CountAsync()).ShouldBe(0);
        await fixture.Interaction.Received(1).RespondAsync($"Logging is enabled again in <#{ChannelId}>.", ephemeral: true);
    }

    [Test]
    public async Task ExclusionsAsync_ListsPersistedExclusions()
    {
        await using BotDbContext db = CreateDbContext(nameof(ExclusionsAsync_ListsPersistedExclusions));
        LoggingInteractionFixture fixture = new LoggingInteractionFixture(db);
        await fixture.Module.ExcludeAsync();
        fixture.Interaction.ClearReceivedCalls();

        await fixture.Module.ExclusionsAsync();

        await fixture.Interaction.Received(1).RespondAsync($"Excluded logging channels:\n- <#{ChannelId}>", ephemeral: true);
    }

    [Test]
    public async Task ConfigAsync_PartialUpdatePreservesOmittedValues()
    {
        await using BotDbContext db = CreateDbContext(nameof(ConfigAsync_PartialUpdatePreservesOmittedValues));
        LoggingInteractionFixture fixture = new LoggingInteractionFixture(db);

        await fixture.Module.ConfigAsync(enabled: true, deleteLogChannel: fixture.DeleteLogChannel, retentionPeriod: 600);
        fixture.Interaction.ClearReceivedCalls();

        await fixture.Module.ConfigAsync(editLogChannel: fixture.EditLogChannel);

        RatBot.Domain.Features.Logging.LoggingConfiguration configuration = await db.LoggingConfigurations.SingleAsync();
        configuration.Enabled.ShouldBeTrue();
        configuration.DeleteLogChannelId.ShouldBe(DeleteLogChannelId);
        configuration.EditLogChannelId.ShouldBe(EditLogChannelId);
        configuration.EvidenceRetentionPeriod.ShouldBe(TimeSpan.FromSeconds(600));
        await fixture
            .Interaction.Received(1)
            .RespondAsync(
                "Logging configuration:\n"
                    + "- Enabled: True\n"
                    + $"- Delete log channel: <#{DeleteLogChannelId}>\n"
                    + $"- Edit log channel: <#{EditLogChannelId}>\n"
                    + "- Evidence retention: 600s",
                ephemeral: true
            );
    }

    [Test]
    public async Task ConfigAsync_WhenEnablingWithoutEitherChannel_Fails()
    {
        await using BotDbContext db = CreateDbContext(nameof(ConfigAsync_WhenEnablingWithoutEitherChannel_Fails));
        LoggingInteractionFixture fixture = new LoggingInteractionFixture(db);

        await fixture.Module.ConfigAsync(enabled: true);

        (await db.LoggingConfigurations.CountAsync()).ShouldBe(0);
        await fixture.Interaction.Received(1).RespondAsync("Enable logging only after setting a delete or edit log channel.", ephemeral: true);
    }

    [Test]
    public async Task LoggingModule_RegistersConfigCommandWithFourOptionalParameters()
    {
        await using BotDbContext db = CreateDbContext(nameof(LoggingModule_RegistersConfigCommandWithFourOptionalParameters));
        using ServiceProvider services = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        InteractionService interactionService = new InteractionService(
            new DiscordSocketClient(),
            new InteractionServiceConfig { AutoServiceScopes = true }
        );

        ModuleInfo module = await interactionService.AddModuleAsync<LoggingModule>(services);
        SlashCommandInfo command = module.SlashCommands.Single(command => string.Equals(command.Name, "config", StringComparison.Ordinal));

        command.Parameters.Select(parameter => parameter.Name).ShouldBe(["enabled", "delete-log-channel", "edit-log-channel", "retention-period"]);
        command.Parameters.Select(parameter => parameter.IsRequired).ShouldBe([false, false, false, false]);
    }

    private static BotDbContext CreateDbContext(string name)
    {
        DbContextOptions<BotDbContext> options = new DbContextOptionsBuilder<BotDbContext>().UseInMemoryDatabase(name).Options;
        return new BotDbContext(options);
    }

    private sealed class LoggingInteractionFixture
    {
        private readonly IInteractionContext _context = Substitute.For<IInteractionContext>();

        public LoggingInteractionFixture(BotDbContext db)
        {
            Guild.Id.Returns(GuildId);
            User.GuildPermissions.Returns(new GuildPermissions(administrator: true));
            Channel.Id.Returns(ChannelId);
            Channel.GuildId.Returns(GuildId);
            Channel.Mention.Returns($"<#{ChannelId}>");
            DeleteLogChannel.Id.Returns(DeleteLogChannelId);
            EditLogChannel.Id.Returns(EditLogChannelId);
            Interaction.HasResponded.Returns(false);
            _context.Guild.Returns(Guild);
            _context.Channel.Returns(Channel);
            _context.User.Returns(User);
            _context.Interaction.Returns(Interaction);

            Module = new LoggingModule(db);
            ((IInteractionModuleBase)Module).SetContext(_context);
        }

        public ITextChannel Channel { get; } = Substitute.For<ITextChannel>();
        public ITextChannel DeleteLogChannel { get; } = Substitute.For<ITextChannel>();
        public ITextChannel EditLogChannel { get; } = Substitute.For<ITextChannel>();
        public IGuild Guild { get; } = Substitute.For<IGuild>();
        public IDiscordInteraction Interaction { get; } = Substitute.For<IDiscordInteraction>();
        public LoggingModule Module { get; }
        public IGuildUser User { get; } = Substitute.For<IGuildUser>();
    }
}
