using System.Text;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed class AdventureLeaderboardComponentBuilder
{

    private static string BuildHeader(AdventureLeaderboardViewModel model) =>
        $"# Practical Python Adventure Leaderboard\n" +
        $"Year {model.Year} • {model.TotalEntrants} entrants • top {model.VisibleEntrants} • updated {FormatTimestamp(model.LastUpdated)}";

    private static string BuildRows(IReadOnlyList<AdventureLeaderboardViewRow> rows)
    {
        if (rows.Count == 0)
            return "_No leaderboard entries yet._";

        StringBuilder text = new StringBuilder();

        foreach (AdventureLeaderboardViewRow row in rows)
            text.AppendLine($"{FormatRank(row.Rank)} `{row.Score}/{row.MaxScore}` `{row.Progress}` {row.DisplayName}");

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
    public MessageComponent Build(AdventureLeaderboardViewModel model)
    {
        ContainerBuilder container = new ContainerBuilder()
            .WithAccentColor(Color.Teal)
            .WithTextDisplay(new TextDisplayBuilder().WithContent(BuildHeader(model)))
            .WithSeparator(new SeparatorBuilder(true, SeparatorSpacingSize.Small))
            .WithTextDisplay(new TextDisplayBuilder().WithContent(BuildRows(model.Rows)));

        if (model.TotalEntrants > model.VisibleEntrants)
        {
            container.WithTextDisplay(
                new TextDisplayBuilder().WithContent($"_Showing top {model.VisibleEntrants} of {model.TotalEntrants} entrants._"));
        }

        return new ComponentBuilderV2([container]).Build();
    }
}
