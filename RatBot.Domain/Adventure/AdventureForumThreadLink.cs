namespace RatBot.Domain.Adventure;

public sealed class AdventureForumThreadLink
{
    private AdventureForumThreadLink() { }

    public ulong GuildId { get; private set; }

    public int ScorePartIndex { get; private set; }

    public ulong ThreadId { get; private set; }

    public static AdventureForumThreadLink Create(ulong guildId, int scorePartIndex, ulong threadId)
    {
        if (guildId == 0)
            throw new ArgumentOutOfRangeException(nameof(guildId), "Guild id is required.");

        if (scorePartIndex is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(scorePartIndex), "Score part index must be between 1 and 20.");

        return new AdventureForumThreadLink
        {
            GuildId = guildId,
            ScorePartIndex = scorePartIndex,
            ThreadId = threadId,
        };
    }
}
