using System.Reflection;
using Discord;
using Discord.Interactions;
using RatBot.Discord.Commands.Settings;
using Shouldly;

namespace RatBot.Discord.Tests.Commands.Settings;

[TestFixture]
public sealed class SettingsInteractionRegistrationTests
{
    [Test]
    public void SetSpambotAsync_HasExpectedSlashCommandMetadata()
    {
        // Arrange
        MethodInfo method =
            typeof(SettingsModule).GetMethod(nameof(SettingsModule.SetSpambotAsync))
            ?? throw new InvalidOperationException("Expected SetSpambotAsync method.");

        // Act
        SlashCommandAttribute slashCommand =
            method.GetCustomAttribute<SlashCommandAttribute>() ?? throw new InvalidOperationException("Expected slash command attribute.");

        ParameterInfo[] parameters = method.GetParameters();

        // Assert
        slashCommand.Name.ShouldBe("spambot");

        parameters.Select(parameter => parameter.ParameterType).ShouldBe(
        [
            typeof(int),
            typeof(int),
        ]);

        parameters.Select(parameter =>
            parameter.GetCustomAttribute<SummaryAttribute>()?.Name).ShouldBe(
        [
            "window",
            "distinct-channel-threshold",
        ]);
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
