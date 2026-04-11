using RatBot.Interactions.Common.Settings;

namespace RatBot.Interactions.Modules.Settings;

[Group("config", "Configuration commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class SettingsModule : SlashCommandBase
{
    [Group("quorum", "Quorum configuration.")]
    public sealed class QuorumSettingsModule(QuorumSettingsService quorumSettingsService)
        : GuildSettingsModuleBase
    {
        [SlashCommand("set", "Create or update a quorum settings for a channel or category ID.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public Task SetAsync(string targetId, string roleIds, double proportion) =>
            ReplyAsync(() => SetResponseAsync(targetId, roleIds, proportion));

        [SlashCommand(
            "unset",
            "Remove a quorum settings for a channel or category. The target ID must be a channel or category ID.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public Task UnsetAsync(string targetId) => ReplyAsync(() => UnsetResponseAsync(targetId));

        private async Task<string> SetResponseAsync(string targetId, string roleIds, double proportion)
        {
            if (!TryResolveQuorumTarget(targetId, out ResolvedQuorumTarget resolvedTarget, out string errorMessage))
                return errorMessage;

            if (!TryResolveRoles(roleIds, out SocketRole[] roles, out errorMessage))
                return errorMessage;

            try
            {
                (bool created, _) = await quorumSettingsService.UpsertAsync(
                    Guild.Id,
                    resolvedTarget.TargetType,
                    resolvedTarget.Channel.Id,
                    roles.Select(role => role.Id).ToArray(),
                    proportion);

                string action = created
                    ? "created"
                    : "updated";

                string roleSummary = string.Join(", ", roles.Select(role => role.Mention));

                return resolvedTarget.Channel switch
                {
                    SocketTextChannel textChannel =>
                        $"Quorum settings {action} for channel {textChannel.Mention} with roles {roleSummary} and proportion {proportion}.",
                    SocketCategoryChannel categoryChannel =>
                        $"Quorum settings {action} for category \"{categoryChannel.Name}\" with roles {roleSummary} and proportion {proportion}.",
                    _ => "Invalid channel type for quorum settings."
                };
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return ex.ParamName switch
                {
                    "value" => "Invalid proportion provided. Please provide a value greater than 0 and at most 1.",
                    "roleIds" => "Invalid role IDs provided. Please provide a comma-separated list of valid role IDs.",
                    "targetType" => "Invalid channel type for quorum settings.",
                    "targetId" => "Invalid target ID provided.",
                    _ => "Invalid quorum settings provided."
                };
            }
        }

        private async Task<string> UnsetResponseAsync(string targetId)
        {
            if (!TryResolveQuorumTarget(targetId, out ResolvedQuorumTarget resolvedTarget, out string errorMessage))
                return errorMessage;

            bool deleted = await quorumSettingsService.DeleteAsync(
                Guild.Id,
                resolvedTarget.TargetType,
                resolvedTarget.Channel.Id);

            if (!deleted)
                return "No quorum settings exist for that target.";

            return resolvedTarget.Channel switch
            {
                SocketTextChannel textChannel => $"Quorum settings removed for channel {textChannel.Mention}.",
                SocketCategoryChannel categoryChannel =>
                    $"Quorum settings removed for category \"{categoryChannel.Name}\".",
                _ => "Invalid channel type for quorum settings."
            };
        }

        private bool TryResolveQuorumTarget(
            string targetId,
            out ResolvedQuorumTarget resolvedTarget,
            out string errorMessage)
        {
            resolvedTarget = null!;

            if (!TryResolveGuildScope(targetId, out SocketGuildChannel? scope, out errorMessage))
                return false;

            QuorumSettingsType? targetType = scope!.ChannelType switch
            {
                ChannelType.Text => QuorumSettingsType.Channel,
                ChannelType.Category => QuorumSettingsType.Category,
                _ => null
            };

            if (targetType is null)
            {
                errorMessage = "Invalid channel type for quorum settings.";
                return false;
            }

            resolvedTarget = new ResolvedQuorumTarget(scope, targetType.Value);
            errorMessage = string.Empty;
            return true;
        }

        private sealed record ResolvedQuorumTarget(SocketGuildChannel Channel, QuorumSettingsType TargetType);
    }
}