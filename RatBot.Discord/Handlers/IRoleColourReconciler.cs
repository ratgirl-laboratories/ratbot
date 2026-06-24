namespace RatBot.Discord.Handlers;

public interface IRoleColourReconciler
{
    Task ReconcileMemberAsync(IGuild guild, ulong userId, CancellationToken ct);
}
