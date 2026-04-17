namespace RatBot.Infrastructure.Persistence.Models;

public sealed class QuorumSettingsRole
{
    public QuorumSettingsRole(ulong id)
    {
        Id = id;
    }

    private QuorumSettingsRole()
    {
    }

    public ulong Id { get; private init; }
}