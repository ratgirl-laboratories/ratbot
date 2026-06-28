using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

public sealed class AdventureLeaderboardComponentBuilder
{
    private const int MaxContainersPerMessage = 5;
    private const int MaxDisplayableTextCharactersPerMessage = 3900;
    private const int MaxRowsPerContainer = 25;

    public static ImmutableArray<MessageComponent> Build(AdventureLeaderboardViewModel model)
    {
        string header = BuildHeader(model);
        ImmutableArray<string> rowTexts = BuildRowTexts(model.Rows);

        if (rowTexts.Length == 0)
            return BuildEmptyLeaderboard(header);

        ImmutableArray<MessageComponent>.Builder messages = ImmutableArray.CreateBuilder<MessageComponent>();
        int rowIndex = 0;
        bool includeHeader = true;

        while (rowIndex < rowTexts.Length)
        {
            ImmutableArray<ContainerBuilder>.Builder containers = ImmutableArray.CreateBuilder<ContainerBuilder>();
            int usedCharacters = includeHeader ? header.Length : 0;

            if (includeHeader)
            {
                ImmutableArray<string> rows = TakeRows(rowTexts, rowIndex, MaxDisplayableTextCharactersPerMessage - usedCharacters);

                containers.Add(BuildHeaderContainer(header, rows));
                usedCharacters += RowsLength(rows);
                rowIndex += rows.Length;
                includeHeader = false;
            }

            while (rowIndex < rowTexts.Length && containers.Count < MaxContainersPerMessage)
            {
                ImmutableArray<string> rows = TakeRows(rowTexts, rowIndex, MaxDisplayableTextCharactersPerMessage - usedCharacters);

                if (rows.Length == 0)
                    break;

                containers.Add(BuildRowsContainer(rows));
                usedCharacters += RowsLength(rows);
                rowIndex += rows.Length;
            }

            messages.Add(BuildMessage(containers.ToImmutableArray()));
        }

        return messages.ToImmutableArray();
    }

    private static ImmutableArray<MessageComponent> BuildEmptyLeaderboard(string header) =>
        ImmutableArray.Create(new ComponentBuilderV2(BuildHeaderContainer(header, ImmutableArray<string>.Empty)).Build());

    private static string BuildHeader(AdventureLeaderboardViewModel model) =>
        $"# Practical Python Adventure Leaderboard\n"
        + $"Year {model.Year} • {model.TotalEntrants} entrants • updated <t:{model.LastUpdated.ToUnixTimeSeconds()}:R>";

    private static ContainerBuilder BuildHeaderContainer(string header, ImmutableArray<string> rows) =>
        new ContainerBuilder()
            .WithAccentColor(Color.Teal)
            .WithTextDisplay(new TextDisplayBuilder().WithContent(header))
            .WithSeparator(new SeparatorBuilder())
            .WithTextDisplay(new TextDisplayBuilder().WithContent(BuildRows(rows)));

    private static MessageComponent BuildMessage(ImmutableArray<ContainerBuilder> containers) => new ComponentBuilderV2(containers).Build();

    private static string BuildRow(AdventureLeaderboardViewRow row) =>
        string.Create(CultureInfo.InvariantCulture, $"{FormatRank(row.Rank)} `{row.Score}/{row.MaxScore}` `{row.Progress}` {row.DisplayName}\n");

    private static string BuildRows(ImmutableArray<string> rows)
    {
        if (rows.Length == 0)
            return "_No leaderboard entries yet._";

        StringBuilder text = new StringBuilder();

        foreach (string row in rows)
            text.Append(row);

        return text.ToString();
    }

    private static ContainerBuilder BuildRowsContainer(ImmutableArray<string> rows) =>
        new ContainerBuilder().WithAccentColor(Color.Teal).WithTextDisplay(new TextDisplayBuilder().WithContent(BuildRows(rows)));

    private static ImmutableArray<string> BuildRowTexts(ImmutableArray<AdventureLeaderboardViewRow> rows) => rows.Select(BuildRow).ToImmutableArray();

    private static string FormatRank(int rank) =>
        rank switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => $"`#{rank:00}`",
        };

    private static int RowsLength(ImmutableArray<string> rows) => rows.Sum(row => row.Length);

    private static ImmutableArray<string> TakeRows(ImmutableArray<string> rows, int startIndex, int availableCharacters)
    {
        ImmutableArray<string>.Builder chunk = ImmutableArray.CreateBuilder<string>();
        int usedCharacters = 0;

        for (int index = startIndex; index < rows.Length && chunk.Count < MaxRowsPerContainer; index++)
        {
            string row = rows[index];

            if (chunk.Count > 0 && usedCharacters + row.Length > availableCharacters)
                break;

            chunk.Add(row);
            usedCharacters += row.Length;

            if (usedCharacters >= availableCharacters)
                break;
        }

        return chunk.ToImmutableArray();
    }
}
