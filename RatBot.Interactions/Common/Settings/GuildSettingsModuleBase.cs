using RatBot.Domain.Common;

namespace RatBot.Interactions.Common.Settings;

public abstract class GuildSettingsModuleBase : SlashCommandBase
{
    protected SocketGuild Guild => Context.Guild!;

    protected bool TryResolveGuildScope(string scopeId, out SocketGuildChannel? scope, out string errorMessage)
    {
        scope = null;

        if (!MentionParser.TryParse(scopeId, out ulong parsedScopeId))
        {
            errorMessage = "Invalid scope ID provided.";
            return false;
        }

        scope = Guild.Channels.FirstOrDefault(channel => channel.Id == parsedScopeId);

        if (scope is null)
        {
            errorMessage = "Invalid scope ID provided.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    protected bool TryResolveRoles(string roleIds, out SocketRole[] roles, out string errorMessage)
    {
        roles = [];

        ulong[] parsedRoleIds = roleIds
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(value => MentionParser.TryParse(value, out ulong parsedRoleId)
                ? parsedRoleId
                : 0)
            .Where(value => value != 0)
            .Distinct()
            .ToArray();

        if (parsedRoleIds.Length == 0)
        {
            errorMessage = "Invalid role IDs provided. Please provide a comma-separated list of valid role IDs.";
            return false;
        }

        roles = parsedRoleIds.Select(Guild.GetRole).Where(role => role is not null).ToArray()!;

        if (roles.Length != parsedRoleIds.Length)
        {
            errorMessage = "One or more role IDs are invalid for this guild.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}