namespace RatBot.Infrastructure.Settings.Quorum;

public sealed class QuorumSettingsRole
{
    private QuorumSettingsRole()
    {
    }

    public QuorumSettingsRole(ulong id)
    {
        Id = id;
    }

    public ulong Id { get; private init; }
}
