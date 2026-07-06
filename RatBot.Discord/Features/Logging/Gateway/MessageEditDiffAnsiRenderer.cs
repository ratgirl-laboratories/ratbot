using System.Text;
using RatBot.Application.Features.Logging;

namespace RatBot.Discord.Features.Logging.Gateway;

internal static class MessageEditDiffAnsiRenderer
{
    private const string Reset = "\e[0m";
    private const string Red = "\e[1;31m";
    private const string Green = "\e[1;32m";

    public static string Render(MessageEditDiff diff)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("Before: ");
        AppendSegments(builder, diff.Before);
        builder.AppendLine();
        builder.Append("After:  ");
        AppendSegments(builder, diff.After);
        return builder.ToString();
    }

    private static void AppendSegments(StringBuilder builder, IReadOnlyList<MessageEditDiffSegment> segments)
    {
        foreach (MessageEditDiffSegment segment in segments)
        {
            string? color = GetColor(segment.Kind);

            if (color is null)
            {
                builder.Append(segment.Text);
                continue;
            }

            builder.Append(color);
            builder.Append(segment.Text);
            builder.Append(Reset);
        }
    }

    private static string? GetColor(MessageEditDiffSegmentKind kind) =>
        kind switch
        {
            MessageEditDiffSegmentKind.Removed => Red,
            MessageEditDiffSegmentKind.Added => Green,
            _ => null,
        };
}
