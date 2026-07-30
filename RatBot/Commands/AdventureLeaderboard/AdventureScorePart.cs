namespace RatBot.Commands.AdventureLeaderboard;

public readonly record struct AdventureScorePart(int Index)
{
    private const int Count = 20;

    public static ImmutableArray<AdventureScorePart> All { get; } =
        Enumerable.Range(1, Count).Select(index => new AdventureScorePart(index)).ToImmutableArray();

    public string ThreadName => $"Week {Week} Part {Part}";

    private int Part => (Index - 1) % 2 + 1;

    private int Week => (Index - 1) / 2 + 1;

    public static ImmutableHashSet<int> CompletedParts(IReadOnlyList<AdventureDayProgress> progress)
    {
        ImmutableHashSet<int>.Builder completed = ImmutableHashSet.CreateBuilder<int>();

        for (int weekIndex = 0; weekIndex < Math.Min(progress.Count, 10); weekIndex++)
        {
            AdventureDayProgress week = progress[weekIndex];
            int baseIndex = weekIndex * 2 + 1;

            if (week.Part1Complete)
                completed.Add(baseIndex);

            if (week.Part2Complete)
                completed.Add(baseIndex + 1);
        }

        return completed.ToImmutable();
    }
}
