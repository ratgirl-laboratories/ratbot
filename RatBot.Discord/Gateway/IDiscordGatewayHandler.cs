namespace RatBot.Discord.Gateway;

public interface IDiscordGatewayHandler
{
    Task InitializeAsync(CancellationToken ct);
    void Unsubscribe();
}
