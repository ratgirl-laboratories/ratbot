namespace RatBot.Domain.Adventure;

public sealed class AdventureLeaderboardMessageState
{
    public const int SingletonId = 1;

    private AdventureLeaderboardMessageState()
    {
    }

    public int Id { get; private set; }

    public ulong GuildId { get; private set; }

    public ulong ChannelId { get; private set; }

    public ulong MessageId { get; private set; }

    public int Year { get; private set; }

    public string LastRenderHash { get; private set; } = string.Empty;

    public static AdventureLeaderboardMessageState Create(
        ulong guildId,
        ulong channelId,
        ulong messageId,
        int year,
        string lastRenderHash) =>
        new AdventureLeaderboardMessageState
        {
            Id = SingletonId,
            GuildId = guildId,
            ChannelId = channelId,
            MessageId = messageId,
            Year = year,
            LastRenderHash = lastRenderHash,
        };
}
