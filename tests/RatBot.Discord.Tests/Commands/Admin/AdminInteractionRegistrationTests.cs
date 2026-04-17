using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RatBot.Application.Administration;
using RatBot.Discord.Commands.Admin;
using Shouldly;

namespace RatBot.Discord.Tests.Commands.Admin;

[TestFixture]
public sealed class AdminInteractionRegistrationTests
{
    [Test]
    public void AdminModule_HasAdministratorGroupMetadata()
    {
        // Arrange
        Type moduleType = typeof(AdminModule);

        // Act
        GroupAttribute group =
            moduleType.GetCustomAttribute<GroupAttribute>()
            ?? throw new InvalidOperationException("Expected admin group attribute.");

        DefaultMemberPermissionsAttribute permissions =
            moduleType.GetCustomAttribute<DefaultMemberPermissionsAttribute>()
            ?? throw new InvalidOperationException("Expected default permissions attribute.");

        // Assert
        group.Name.ShouldBe("admin");
        group.Description.ShouldBe("Administrative commands.");
        permissions.Permissions.ShouldBe(GuildPermission.Administrator);
    }

    [Test]
    public void SendAsync_HasExpectedSlashCommandMetadata()
    {
        // Arrange
        MethodInfo sendMethod =
            typeof(AdminModule).GetMethod(nameof(AdminModule.SendAsync))
            ?? throw new InvalidOperationException("Expected SendAsync method.");

        // Act
        SlashCommandAttribute slashCommand =
            sendMethod.GetCustomAttribute<SlashCommandAttribute>()
            ?? throw new InvalidOperationException("Expected slash command attribute.");

        RequireUserPermissionAttribute userPermission =
            sendMethod.GetCustomAttribute<RequireUserPermissionAttribute>()
            ?? throw new InvalidOperationException("Expected user permission attribute.");

        // Assert
        slashCommand.Name.ShouldBe("send");
        slashCommand.Description.ShouldBe("Send a multiline message as the bot to a specific channel.");
        userPermission.GuildPermission.ShouldBe(GuildPermission.Administrator);
    }

    [Test]
    public void SendModalAsync_HasExpectedModalMetadata()
    {
        // Arrange
        MethodInfo modalMethod =
            typeof(AdminModule).GetMethod(nameof(AdminModule.SendModalAsync))
            ?? throw new InvalidOperationException("Expected SendModalAsync method.");

        // Act
        ModalInteractionAttribute modalInteraction =
            modalMethod.GetCustomAttribute<ModalInteractionAttribute>()
            ?? throw new InvalidOperationException("Expected modal interaction attribute.");

        RequireUserPermissionAttribute userPermission =
            modalMethod.GetCustomAttribute<RequireUserPermissionAttribute>()
            ?? throw new InvalidOperationException("Expected user permission attribute.");

        // Assert
        modalInteraction.CustomId.ShouldBe("admin-send:*:*");
        modalInteraction.IgnoreGroupNames.ShouldBeTrue();
        modalInteraction.TreatAsRegex.ShouldBeFalse();
        userPermission.GuildPermission.ShouldBe(GuildPermission.Administrator);
    }

    [Test]
    public void AdminSendModal_HasExpectedMessageInputMetadata()
    {
        // Arrange
        AdminSendModal modal = new AdminSendModal { Message = "hello" };

        PropertyInfo messageProperty =
            typeof(AdminSendModal).GetProperty(nameof(AdminSendModal.Message))
            ?? throw new InvalidOperationException("Expected Message property.");

        // Act
        InputLabelAttribute inputLabel =
            messageProperty.GetCustomAttribute<InputLabelAttribute>()
            ?? throw new InvalidOperationException("Expected input label attribute.");

        ModalTextInputAttribute textInput =
            messageProperty.GetCustomAttribute<ModalTextInputAttribute>()
            ?? throw new InvalidOperationException("Expected text input attribute.");

        // Assert
        modal.Title.ShouldBe("Send message as ratbot to a channel");
        inputLabel.Label.ShouldBe("Message");
        textInput.CustomId.ShouldBe("message");
        textInput.Style.ShouldBe(TextInputStyle.Paragraph);
        textInput.Placeholder.ShouldBe("Message to send as the bot.");
        textInput.MaxLength.ShouldBe(4000);
    }

    [Test]
    public async Task InteractionService_DiscoversAdminModuleAndSendCommands()
    {
        // Arrange
        ServiceProvider services = new ServiceCollection().AddScoped<AdminSendService>().BuildServiceProvider();

        InteractionService interactionService = new InteractionService(
            new DiscordSocketClient(),
            new InteractionServiceConfig { AutoServiceScopes = true });

        // Act
        await interactionService.AddModuleAsync<AdminModule>(services);

        // Assert
        ModuleInfo adminModule = interactionService.Modules.Single(module => module.SlashGroupName == "admin");

        adminModule.SlashCommands.Single(command => command.Name == "send").ShouldNotBeNull();
        adminModule.ModalCommands.Single(command => command.Name == "admin-send:*:*").ShouldNotBeNull();
    }
}
