namespace RatBot.Discord.Features.Logging.Gateway;

public sealed class SerilogTelemetryHandler : InteractionModuleBase<IInteractionContext>
{
    public const string Id = "accept-joy";

    [ComponentInteraction(Id)]
    public async Task Respond() => await RespondAsync($"Thank you for enjoying, <@{Context.User.Id}> ☺️", ephemeral: true);
}
