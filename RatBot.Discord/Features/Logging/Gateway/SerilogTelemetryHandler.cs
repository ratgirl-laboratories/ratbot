namespace RatBot.Discord.Features.Logging.Gateway;

public sealed class SerilogTelemetryHandler : InteractionModuleBase<IInteractionContext>
{
    public const string Id = "accept-joy";

    [ComponentInteraction(Id)]
    public async Task Respond() => await RespondAsync($"May joye befall thee, by this Robynes passyng, <@{Context.User.Id}> ☺️", ephemeral: true);
}
