namespace RatBot.Interactions.Modules.General;

[UsedImplicitly]
public sealed class HelloModule(ILogger logger) : SlashCommandBase
{
    private readonly ILogger _logger = logger.ForContext<HelloModule>();

    [SlashCommand("hello", "Says hello!")]
    [RequireUserPermission(GuildPermission.SendMessages)]
    public Task HelloAsync()
    {
        _logger.Information("Received hello command from {User}", Context.User.Username);
        return ReplyAsync($"Hello, {Context.User.Mention}!");
    }
}