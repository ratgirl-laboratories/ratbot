using System.Collections.Immutable;
using Discord.Rest;
using RatBot.Application.Meta;

namespace RatBot.Discord.Commands.Settings;

[Group("config", "Configuration commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class SettingsModule : SlashCommandBase
{
    [Group("meta", "Meta configuration.")]
    public sealed class MetaSettingsModule(MetaSuggestionSettingsService metaSuggestionSettingsService)
        : SlashCommandBase
    {
        [SlashCommand("suggest", "Set the forum channel used for `/meta suggest` submissions.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetSuggestForumChannelAsync(IForumChannel channel)
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command can only be used in a guild.", ephemeral: true);
                return;
            }

            ErrorOr<Success> result = await metaSuggestionSettingsService
                .UpsertSuggestForumChannelAsync(Context.Guild.Id, channel.Id);

            await result.SwitchFirstAsync(
                async _ =>
                    await RespondAsync(
                        $"Meta suggestion forum set to {channel.Mention}.",
                        ephemeral: true),
                async error => await RespondAsync(error.Description, ephemeral: true));
        }
    }

    [Group("quorum", "Quorum configuration.")]
    public sealed class QuorumSettingsModule(QuorumSettingsService quorumSettingsService) : SlashCommandBase
    {
        [SlashCommand("set", "Create or update a quorum settings for a channel or category ID.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetAsync(string targetId, string roleIdStrings, double proportion)
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command can only be used in a guild.", ephemeral: true);
                return;
            }

            ErrorOr<ResolvedQuorumTarget> resolvedTargetResult = ResolveQuorumTarget(targetId);

            if (resolvedTargetResult.IsError)
            {
                await RespondAsync(resolvedTargetResult.FirstError.Description, ephemeral: true);
                return;
            }

            Queue<ulong> roleIds = [];

            foreach (string roleString in roleIdStrings.Split(',', StringSplitOptions.TrimEntries))
            {
                if (MentionUtils.TryParseRole(roleString, out ulong roleId))
                {
                    roleIds.Enqueue(roleId);
                    continue;
                }

                await RespondAsync("Invalid role ID provided.", ephemeral: true);
                return;
            }

            ResolvedQuorumTarget resolvedTarget = resolvedTargetResult.Value;
            ImmutableArray<RestRole>.Builder rolesBuilder = ImmutableArray.CreateBuilder<RestRole>();

            try
            {
                rolesBuilder.AddRange(
                    (await Task.WhenAll(roleIds.Select(roleId => Context.Guild.GetRoleAsync(roleId))))
                    .Where(role => role is not null));
            }
            catch (Exception ex)
            {
                await RespondAsync($"Failed to fetch roles: {ex.Message}", ephemeral: true);
                return;
            }
            
            ImmutableArray<RestRole> roles = rolesBuilder.ToImmutable();

            ErrorOr<QuorumSettingsUpsertResult> upsertResult = await quorumSettingsService.UpsertAsync(
                Context.Guild.Id,
                resolvedTarget.TargetType,
                resolvedTarget.Channel.Id,
                roles.Select(role => role.Id),
                proportion);

            await upsertResult.SwitchFirstAsync(
                onValue: async result => await RespondAsync(
                    BuildSetResponse(resolvedTarget.Channel, rolesBuilder, result.Created, proportion),
                    ephemeral: true),
                onFirstError: async error => await RespondAsync(DescribeError(error), ephemeral: true));
        }

        [SlashCommand(
            "unset",
            "Remove a quorum settings for a channel or category. The target ID must be a channel or category ID.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task UnsetAsync(string targetId)
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command can only be used in a guild.", ephemeral: true);
                return;
            }

            ErrorOr<ResolvedQuorumTarget> resolvedTargetResult = ResolveQuorumTarget(targetId);

            if (resolvedTargetResult.IsError)
            {
                await RespondAsync(resolvedTargetResult.FirstError.Description, ephemeral: true);
                return;
            }

            ResolvedQuorumTarget resolvedTarget = resolvedTargetResult.Value;

            ErrorOr<Deleted> deleteResult = await quorumSettingsService.DeleteAsync(
                Context.Guild.Id,
                resolvedTarget.TargetType,
                resolvedTarget.Channel.Id);

            await deleteResult.SwitchFirstAsync(
                async _ => await RespondAsync(BuildUnsetResponse(resolvedTarget.Channel), ephemeral: true),
                async error => await RespondAsync(DescribeError(error), ephemeral: true));
        }

        private static string BuildSetResponse(
            SocketGuildChannel channel,
            IReadOnlyCollection<IRole> roles,
            bool created,
            double proportion)
        {
            string action = created
                ? "created"
                : "updated";

            string roleSummary = string.Join(", ", roles.Select(role => role.Mention));

            return channel switch
            {
                SocketTextChannel textChannel =>
                    $"Quorum settings {action} for channel {textChannel.Mention} with roles {roleSummary} and proportion {proportion}.",
                SocketCategoryChannel categoryChannel =>
                    $"Quorum settings {action} for category \"{categoryChannel.Name}\" with roles {roleSummary} and proportion {proportion}.",
                _ => "Invalid channel type for quorum settings."
            };
        }

        private static string BuildUnsetResponse(SocketGuildChannel channel) =>
            channel switch
            {
                SocketTextChannel textChannel => $"Quorum settings removed for channel {textChannel.Mention}.",
                SocketCategoryChannel categoryChannel =>
                    $"Quorum settings removed for category \"{categoryChannel.Name}\".",
                _ => "Invalid channel type for quorum settings removal."
            };

        private static string DescribeError(Error error) =>
            error.Type switch
            {
                ErrorType.NotFound => "No quorum settings exist for that target.",
                _ => error.Description
            };

        private ErrorOr<ResolvedQuorumTarget> ResolveQuorumTarget(string targetId)
        {
            ErrorOr<SocketGuildChannel> scopeResult = MentionUtils.TryParseChannel(targetId, out ulong channelId)
                ? ErrorOrFactory.From(Context.Guild.GetChannel(channelId))
                : Error.Validation(description: "Invalid channel ID provided.");

            if (scopeResult.IsError)
                return scopeResult.Errors;

            SocketGuildChannel scope = scopeResult.Value;

            QuorumSettingsType? targetType = scope.ChannelType switch
            {
                ChannelType.Text => QuorumSettingsType.Channel,
                ChannelType.Category => QuorumSettingsType.Category,
                _ => null
            };

            if (targetType is null)
                return Error.Validation(description: "Invalid channel type for quorum settings.");

            return new ResolvedQuorumTarget(scope, targetType.Value);
        }

        private sealed record ResolvedQuorumTarget(SocketGuildChannel Channel, QuorumSettingsType TargetType);
    }
}