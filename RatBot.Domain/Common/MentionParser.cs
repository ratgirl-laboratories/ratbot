using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace RatBot.Domain.Common;

[UsedImplicitly]
public partial class MentionParser
{
    public static bool TryParse(string mentionString, out ulong id)
    {
        if (string.IsNullOrWhiteSpace(mentionString))
        {
            id = 0;
            return false;
        }

        if (ulong.TryParse(mentionString, out id))
            return true;

        Match match = MentionPattern().Match(mentionString);

        if (match.Success)
        {
            id = ulong.Parse(match.Groups[1].Value);
            return true;
        }

        id = 0;
        return false;
    }

    [GeneratedRegex(@"<[#@][!&]?(\d+)>")]
    private static partial Regex MentionPattern();
}