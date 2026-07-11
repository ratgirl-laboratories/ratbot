namespace RatBot.Discord.Features.Logging.Gateway;

public sealed class SerilogTelemetryHandler(ILogger logger) : InteractionModuleBase<IInteractionContext>
{
    private readonly ILogger _logger = logger.ForContext<SerilogTelemetryHandler>();

    public const string AcceptId = "accept-joy";
    public const string DeclineId = "decline-joy";

    [ComponentInteraction(AcceptId)]
    public async Task Respond()
    {
        await RespondAsync($"May joye befall thee, by this Robynes passyng, <@{Context.User.Id}> ☺️", ephemeral: true);
        _logger.Information("responded to {UserId}", Context.User.Id);
    }

    [ComponentInteraction(DeclineId)]
    public async Task Decline()
    {
        await RespondAsync($"The Robyne passeth onward, troubling thee no further, <@{Context.User.Id}> ☺️", ephemeral: true);
        _logger.Information("declined by {UserId}", Context.User.Id);
    }
}
