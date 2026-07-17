namespace RatBot.Domain.Adventure;

public sealed class AdventureSettings
{
    private AdventureSettings() { }

    public ulong AdventurerRoleId { get; private set; }

    public ulong GuildId { get; private set; }

    public static AdventureSettings Create(ulong guildId, ulong adventurerRoleId)
    {
        if (guildId == 0)
            throw new ArgumentOutOfRangeException(nameof(guildId), "Guild id is required.");

        if (adventurerRoleId == 0)
            throw new ArgumentOutOfRangeException(nameof(adventurerRoleId), "Adventurer role id is required.");

        return new AdventureSettings { GuildId = guildId, AdventurerRoleId = adventurerRoleId };
    }

    public void UpdateAdventurerRole(ulong adventurerRoleId)
    {
        if (adventurerRoleId == 0)
            throw new ArgumentOutOfRangeException(nameof(adventurerRoleId), "Adventurer role id is required.");

        AdventurerRoleId = adventurerRoleId;
    }
}
