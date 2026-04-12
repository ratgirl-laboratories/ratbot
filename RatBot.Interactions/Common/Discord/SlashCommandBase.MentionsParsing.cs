using System.Text.RegularExpressions;

namespace RatBot.Interactions.Common.Discord;

public abstract partial class SlashCommandBase
{

    private enum MentionKind
    {
        Channel,
        Role,
        User
    }
    private static ErrorOr<ulong> ParseMentionId(
        string mentionString,
        MentionKind expectedKind,
        string invalidMentionDescription)
    {
        if (string.IsNullOrWhiteSpace(mentionString))
            return Error.Validation(description: invalidMentionDescription);

        if (ulong.TryParse(mentionString, out ulong id))
            return id;

        Match match = MentionPattern().Match(mentionString);

        if (!match.Success)
            return Error.Validation(description: invalidMentionDescription);

        MentionKind? actualKind = match.Groups["kind"].Value switch
        {
            "#" => MentionKind.Channel,
            "@&" => MentionKind.Role,
            "@" or "@!" => MentionKind.User,
            _ => null
        };

        if (actualKind is null && actualKind != expectedKind)
            return Error.Validation(description: invalidMentionDescription);

        if (!ulong.TryParse(match.Groups["id"].Value, out id))
            return Error.Validation(description: invalidMentionDescription);

        return id;
    }

    [GeneratedRegex(@"^<(?<kind>#|@&|@!?)(?<id>\d+)>$")]
    private static partial Regex MentionPattern();

    protected ErrorOr<SocketGuildChannel> ResolveGuildChannel(
        string channelId,
        string invalidMentionDescription,
        string unresolvedMentionDescription)
    {
        ErrorOr<ulong> idResult = ParseMentionId(channelId, MentionKind.Channel, invalidMentionDescription);

        if (idResult.IsError)
            return idResult.Errors;

        SocketGuildChannel? channel = Context.Guild.Channels.FirstOrDefault(channel => channel.Id == idResult.Value);

        if (channel is null)
            return Error.Validation(description: unresolvedMentionDescription);

        return channel;
    }

    protected ErrorOr<SocketRole[]> ResolveGuildRoles(
        string roleIds,
        string invalidMentionDescription,
        string unresolvedMentionDescription)
    {
        string[] values = roleIds.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (values.Length == 0)
            return Error.Validation(description: invalidMentionDescription);

        List<SocketRole> roles = [];
        HashSet<ulong> resolvedRoleIds = [];

        foreach (string value in values)
        {
            ErrorOr<ulong> idResult = ParseMentionId(value, MentionKind.Role, invalidMentionDescription);

            if (idResult.IsError)
                return idResult.Errors;

            if (!resolvedRoleIds.Add(idResult.Value))
                continue;

            SocketRole? role = Context.Guild.GetRole(idResult.Value);

            if (role is null)
                return Error.Validation(description: unresolvedMentionDescription);

            roles.Add(role);
        }

        return roles.ToArray();
    }
}