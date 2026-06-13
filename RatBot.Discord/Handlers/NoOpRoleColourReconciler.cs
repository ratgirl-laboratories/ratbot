namespace RatBot.Discord.Handlers;

/// <summary>
///     Temporary placeholder reconciler. Replace with real implementation that derives DCRs from preference and SCRs.
/// </summary>
public sealed class NoOpRoleColourReconciler : IRoleColourReconciler
{
    public Task ReconcileMemberAsync(IGuild guild, ulong userId, CancellationToken ct) => Task.CompletedTask;
}