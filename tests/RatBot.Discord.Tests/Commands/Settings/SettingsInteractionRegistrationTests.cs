using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RatBot.Application.Moderation;
using RatBot.Discord.Commands.Spam;
using RatBot.Discord.Commands.Settings;
using RatBot.Domain.Moderation;
using Shouldly;

namespace RatBot.Discord.Tests.Commands.Settings;

[TestFixture]
public sealed class SettingsInteractionRegistrationTests
{
    [Test]
    public void ImageSpamConfigAsync_HasExpectedSlashCommandMetadata()
    {
        // Arrange
        MethodInfo method =
            typeof(SpamModule).GetMethod(nameof(SpamModule.ImageSpamConfigAsync))
            ?? throw new InvalidOperationException("Expected ImageSpamConfigAsync method.");

        // Act
        GroupAttribute group =
            typeof(SpamModule).GetCustomAttribute<GroupAttribute>()
            ?? throw new InvalidOperationException("Expected group attribute.");

        SlashCommandAttribute slashCommand =
            method.GetCustomAttribute<SlashCommandAttribute>() ?? throw new InvalidOperationException("Expected slash command attribute.");

        ParameterInfo[] parameters = method.GetParameters();

        // Assert
        group.Name.ShouldBe("spam");
        slashCommand.Name.ShouldBe("image-spam-config");

        parameters.Select(parameter => parameter.ParameterType).ShouldBe(
        [
            typeof(int?),
            typeof(int?),
            typeof(int?),
        ]);

        parameters.Select(parameter =>
            parameter.GetCustomAttribute<SummaryAttribute>()?.Name).ShouldBe(
        [
            "number-of-channels",
            "number-of-required-attached-messages",
            "burst-duration",
        ]);

        parameters.Select(parameter => parameter.HasDefaultValue && parameter.DefaultValue is null).ShouldBe(
        [
            true,
            true,
            true,
        ]);
    }

    [Test]
    public async Task ImageSpamConfigAsync_CanBeRegisteredByInteractionService()
    {
        // Arrange
        DiscordSocketClient client = new DiscordSocketClient();
        InteractionService interactionService = new InteractionService(client);
        ServiceProvider services = new ServiceCollection()
            .AddSingleton<ImageBurstSpamDetectorSettings>()
            .AddSingleton<IImageSpamSettingsStore, FakeImageSpamSettingsStore>()
            .AddScoped<ImageSpamSettingsService>()
            .BuildServiceProvider();

        // Act
        ModuleInfo module = await interactionService.AddModuleAsync<SpamModule>(services);
        SlashCommandInfo command = module.SlashCommands.Single();

        // Assert
        command.Name.ShouldBe("image-spam-config");
        command.Parameters.Select(parameter => parameter.IsRequired).ShouldBe([false, false, false]);
    }

    private sealed class FakeImageSpamSettingsStore : IImageSpamSettingsStore
    {
        public Task<ImageSpamSettings?> GetAsync(CancellationToken ct) => Task.FromResult<ImageSpamSettings?>(null);

        public Task<ImageSpamSettings> UpsertAsync(
            int? requiredChannelCount,
            int? requiredAttachedMessageCount,
            int? burstDurationSeconds,
            CancellationToken ct) =>
            Task.FromResult(ImageSpamSettings.CreateDefault());
    }

    [Test]
    public void MetaSettingsModule_HasExpectedGroupMetadata()
    {
        // Arrange
        Type moduleType = typeof(SettingsModule.MetaSettingsModule);

        // Act
        GroupAttribute group =
            moduleType.GetCustomAttribute<GroupAttribute>() ?? throw new InvalidOperationException("Expected meta group attribute.");

        // Assert
        group.Name.ShouldBe("meta");
        group.Description.ShouldBe("Meta configuration.");
    }

    [Test]
    public void SetSuggestForumChannelAsync_HasForumChannelParameter()
    {
        // Arrange
        MethodInfo method =
            typeof(SettingsModule.MetaSettingsModule).GetMethod(nameof(SettingsModule.MetaSettingsModule.SetSuggestForumChannelAsync))
            ?? throw new InvalidOperationException("Expected SetSuggestForumChannelAsync method.");

        // Act
        ParameterInfo parameter = method.GetParameters().Single();

        // Assert
        parameter.ParameterType.ShouldBe(typeof(IForumChannel));

        SlashCommandAttribute slashCommand =
            method.GetCustomAttribute<SlashCommandAttribute>() ?? throw new InvalidOperationException("Expected slash command attribute.");

        slashCommand.Name.ShouldBe("suggest");
    }
}
