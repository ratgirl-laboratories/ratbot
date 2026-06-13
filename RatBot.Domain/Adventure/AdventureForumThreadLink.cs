namespace RatBot.Domain.Adventure;

public sealed class AdventureForumThreadLink
{
    private AdventureForumThreadLink()
    {
    }

    public int ScorePartIndex { get; private set; }

    public ulong ThreadId { get; private set; }

    public static AdventureForumThreadLink Create(int scorePartIndex, ulong threadId)
    {
        if (scorePartIndex is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(scorePartIndex), "Score part index must be between 1 and 20.");

        return new AdventureForumThreadLink
        {
            ScorePartIndex = scorePartIndex,
            ThreadId = threadId,
        };
    }
}