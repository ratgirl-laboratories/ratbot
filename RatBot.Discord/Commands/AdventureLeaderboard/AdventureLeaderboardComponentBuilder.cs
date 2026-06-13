using System.Globalization;
using System.Text;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed class AdventureLeaderboardComponentBuilder
{
    private const int MaxRowsPerContainer = 25;
    private const int MaxContainersPerMessage = 5;

    private static string BuildHeader(AdventureLeaderboardViewModel model) =>
        $"# Practical Python Adventure Leaderboard\n"
        + $"Year {model.Year} • {model.TotalEntrants} entrants • updated {FormatTimestamp(model.LastUpdated)}";

    private static string BuildRows(IReadOnlyList<AdventureLeaderboardViewRow> rows)
    {
        if (rows.Count == 0)
            return "_No leaderboard entries yet._";

        StringBuilder text = new StringBuilder();

        foreach (AdventureLeaderboardViewRow row in rows)
            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"{FormatRank(row.Rank)} `{row.Score}/{row.MaxScore}` `{row.Progress}` {row.DisplayName}");

        return text.ToString();
    }

    private static string FormatRank(int rank) =>
        rank switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => $"`#{rank:00}`",
        };

    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        $"<t:{timestamp.ToUnixTimeSeconds()}:R>";

    private static List<ContainerBuilder> BuildContainers(AdventureLeaderboardViewModel model)
    {
        List<ContainerBuilder> containers = new List<ContainerBuilder>();
        IReadOnlyList<IReadOnlyList<AdventureLeaderboardViewRow>> rowChunks = ChunkRows(model.Rows);

        if (rowChunks.Count == 0)
        {
            containers.Add(
                new ContainerBuilder()
                    .WithAccentColor(Color.Teal)
                    .WithTextDisplay(new TextDisplayBuilder().WithContent(BuildHeader(model)))
                    .WithSeparator(new SeparatorBuilder())
                    .WithTextDisplay(new TextDisplayBuilder().WithContent(BuildRows(model.Rows))));

            return containers;
        }

        containers.Add(
            new ContainerBuilder()
                .WithAccentColor(Color.Teal)
                .WithTextDisplay(new TextDisplayBuilder().WithContent(BuildHeader(model)))
                .WithSeparator(new SeparatorBuilder())
                .WithTextDisplay(new TextDisplayBuilder().WithContent(BuildRows(rowChunks[0]))));

        foreach (IReadOnlyList<AdventureLeaderboardViewRow> rows in rowChunks.Skip(1))
            containers.Add(
                new ContainerBuilder()
                    .WithAccentColor(Color.Teal)
                    .WithTextDisplay(new TextDisplayBuilder().WithContent(BuildRows(rows))));

        return containers;
    }

    private static IReadOnlyList<IReadOnlyList<AdventureLeaderboardViewRow>> ChunkRows(
        IReadOnlyList<AdventureLeaderboardViewRow> rows)
    {
        List<IReadOnlyList<AdventureLeaderboardViewRow>> chunks =
            new List<IReadOnlyList<AdventureLeaderboardViewRow>>();

        for (int index = 0; index < rows.Count; index += MaxRowsPerContainer)
            chunks.Add(rows.Skip(index).Take(MaxRowsPerContainer).ToArray());

        return chunks;
    }

    public IReadOnlyList<MessageComponent> Build(AdventureLeaderboardViewModel model)
    {
        List<ContainerBuilder> containers = BuildContainers(model);
        List<MessageComponent> components = new List<MessageComponent>();

        for (int index = 0; index < containers.Count; index += MaxContainersPerMessage)
            components.Add(
                new ComponentBuilderV2(
                        containers.Skip(index).Take(MaxContainersPerMessage))
                    .Build());

        return components;
    }
}