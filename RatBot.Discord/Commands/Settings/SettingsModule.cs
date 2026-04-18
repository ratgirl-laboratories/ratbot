using System.Collections.Immutable;
using System.Globalization;
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
            ErrorOr<Success> result =
                await metaSuggestionSettingsService.UpsertSuggestForumChannelAsync(Context.Guild.Id, channel.Id);

            await result.SwitchFirstAsync(
                async _ => await RespondAsync($"Meta suggestion forum set to {channel.Mention}.", ephemeral: true),
                async error => await RespondAsync(error.Description, ephemeral: true)
            );
        }
    }

    [Group("quorum", "Quorum configuration.")]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    public sealed class QuorumSettingsModule(QuorumSettingsService quorumSettingsService) : SlashCommandBase
    {
        [SlashCommand("set", "Create or update quorum settings for a channel or category.")]
        public async Task SetAsync(
            [Summary("target", "The target channel or category.")]
            [ChannelTypes(ChannelType.Text, ChannelType.Category)]
            IChannel target,
            [Summary("roles", "Comma-separated role mentions or role IDs.")]
            string roles,
            [Summary("proportion", "The quorum proportion, from 0 to 1.")]
            double proportion
        )
        {
            if (target is not SocketGuildChannel guildChannel)
            {
                await RespondAsync("Invalid guild channel provided.", ephemeral: true);
                return;
            }

            ErrorOr<ResolvedQuorumTarget> resolvedTargetResult = ResolveQuorumTarget(guildChannel);

            if (resolvedTargetResult.IsError)
            {
                await RespondAsync(resolvedTargetResult.FirstError.Description, ephemeral: true);
                return;
            }

            if (double.IsNaN(proportion) || double.IsInfinity(proportion) || proportion is <= 0 or > 1)
            {
                await RespondAsync("Quorum proportion must be greater than 0 and at most 1.", ephemeral: true);
                return;
            }

            ErrorOr<ImmutableArray<SocketRole>> parsedRolesResult = ParseRolesCsv(Context.Guild, roles);

            if (parsedRolesResult.IsError)
            {
                await RespondAsync(parsedRolesResult.FirstError.Description, ephemeral: true);
                return;
            }

            ResolvedQuorumTarget resolvedTarget = resolvedTargetResult.Value;
            ImmutableArray<SocketRole> resolvedRoles = parsedRolesResult.Value;

            ErrorOr<QuorumSettingsUpsertResult> upsertResult = await quorumSettingsService.UpsertAsync(
                Context.Guild.Id,
                resolvedTarget.TargetType,
                resolvedTarget.Channel.Id,
                resolvedRoles.Select(role => role.Id),
                proportion
            );

            await upsertResult.SwitchFirstAsync(
                async result =>
                    await RespondAsync(
                        BuildSetResponse(resolvedTarget.Channel, resolvedRoles, result.Created, proportion),
                        ephemeral: true),
                async error => await RespondAsync(DescribeError(error), ephemeral: true)
            );
        }

        [SlashCommand(
            "unset",
            "Remove a quorum settings for a channel or category. The target ID must be a channel or category ID.")]
        public async Task UnsetAsync(
            [Summary("target", "The target channel or category.")]
            [ChannelTypes(ChannelType.Text, ChannelType.Category)]
            IChannel target
        )
        {
            if (Context.Guild is null)
            {
                await RespondAsync("This command can only be used in a guild.", ephemeral: true);
                return;
            }

            if (target is not SocketGuildChannel targetChannel)
            {
                await RespondAsync("Invalid guild channel provided.", ephemeral: true);
                return;
            }

            ErrorOr<ResolvedQuorumTarget> resolvedTargetResult = ResolveQuorumTarget(targetChannel);

            if (resolvedTargetResult.IsError)
            {
                await RespondAsync(resolvedTargetResult.FirstError.Description, ephemeral: true);
                return;
            }

            ResolvedQuorumTarget resolvedTarget = resolvedTargetResult.Value;

            ErrorOr<Deleted> deleteResult = await quorumSettingsService.DeleteAsync(
                Context.Guild.Id,
                resolvedTarget.TargetType,
                resolvedTarget.Channel.Id
            );

            await deleteResult.SwitchFirstAsync(
                async _ => await RespondAsync(BuildUnsetResponse(resolvedTarget.Channel), ephemeral: true),
                async error => await RespondAsync(DescribeError(error), ephemeral: true)
            );
        }

        [SlashCommand("view", "View the quorum settings for a channel or category.")]
        public async Task ViewAsync(
            [Summary("target", "The target channel or category.")]
            [ChannelTypes(ChannelType.Text, ChannelType.Category)]
            IChannel target
        )
        {
            if (target is not SocketGuildChannel targetChannel)
            {
                await RespondAsync("Invalid guild channel provided.", ephemeral: true);
                return;
            }

            ErrorOr<ResolvedQuorumTarget> resolvedTargetResult = ResolveQuorumTarget(targetChannel);

            if (resolvedTargetResult.IsError)
            {
                await RespondAsync(resolvedTargetResult.FirstError.Description, ephemeral: true);
                return;
            }

            ResolvedQuorumTarget resolvedTarget = resolvedTargetResult.Value;

            ErrorOr<QuorumSettings> getResult = await quorumSettingsService.GetAsync(
                Context.Guild.Id,
                resolvedTarget.TargetType,
                resolvedTarget.Channel.Id
            );

            await getResult.SwitchFirstAsync(
                async settings =>
                {
                    SocketRole[] resolvedRoles = settings
                        .Roles.Select(role => Context.Guild.GetRole(role.Id))
                        .Where(role => role is not null)
                        .Select(role => role!)
                        .ToArray();

                    await RespondAsync(
                        BuildViewResponse(resolvedTarget.Channel, resolvedRoles, settings.QuorumProportion),
                        ephemeral: true);
                },
                async error => await RespondAsync(DescribeError(error), ephemeral: true)
            );
        }

        private static string BuildSetResponse(
            SocketGuildChannel channel,
            IReadOnlyCollection<SocketRole> roles,
            bool created,
            double proportion)
        {
            string action = created
                ? "created"
                : "updated";

            string renderedRoles = string.Join(", ", roles.Select(role => role.Mention));
            string renderedProportion = FormatProportion(proportion);

            return channel switch
            {
                SocketTextChannel textChannel =>
                    $"Quorum settings {action} for channel {textChannel.Mention} with roles {renderedRoles} and proportion {renderedProportion}.",
                SocketCategoryChannel categoryChannel =>
                    $"Quorum settings {action} for category \"{categoryChannel.Name}\" with roles {renderedRoles} and proportion {renderedProportion}.",
                _ => "Invalid channel type for quorum settings."
            };
        }

        private static string BuildViewResponse(
            SocketGuildChannel channel,
            IReadOnlyCollection<SocketRole> roles,
            double proportion)
        {
            string renderedRoles = roles.Count == 0
                ? "none"
                : string.Join(", ", roles.Select(role => role.Mention));

            string renderedProportion = FormatProportion(proportion);

            return channel switch
            {
                SocketTextChannel textChannel =>
                    $"Quorum settings for channel {textChannel.Mention}: roles {renderedRoles}; proportion {renderedProportion}.",
                SocketCategoryChannel categoryChannel =>
                    $"Quorum settings for category \"{categoryChannel.Name}\": roles {renderedRoles}; proportion {renderedProportion}.",
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

        private static ErrorOr<ResolvedQuorumTarget> ResolveQuorumTarget(SocketGuildChannel channel)
        {
            QuorumSettingsType? targetType = channel.ChannelType switch
            {
                ChannelType.Text => QuorumSettingsType.Channel,
                ChannelType.Category => QuorumSettingsType.Category,
                _ => null
            };

            return targetType is null
                ? Error.Validation(description: "Invalid channel type for quorum settings.")
                : new ResolvedQuorumTarget(channel, targetType.Value);
        }

        private static ErrorOr<ImmutableArray<SocketRole>> ParseRolesCsv(SocketGuild guild, string roles)
        {
            string[] entries = roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (entries.Length == 0)
                return Error.Validation(description: "At least one role must be provided.");

            HashSet<ulong> deduplicatedRoleIds = [];
            ImmutableArray<SocketRole>.Builder resolvedRoles = ImmutableArray.CreateBuilder<SocketRole>();

            foreach (string entry in entries)
            {
                ulong roleId;

                if (MentionUtils.TryParseRole(entry, out ulong mentionRoleId))
                    roleId = mentionRoleId;
                else if (ulong.TryParse(entry, out ulong parsedRoleId))
                    roleId = parsedRoleId;
                else
                    return Error.Validation(description: $"Invalid role entry: \"{entry}\".");

                if (!deduplicatedRoleIds.Add(roleId))
                    continue;

                SocketRole? role = guild.GetRole(roleId);

                if (role is null)
                    return Error.Validation(description: $"Role not found in this guild: \"{entry}\".");

                resolvedRoles.Add(role);
            }

            return resolvedRoles.ToImmutable();
        }

        private static string FormatProportion(double proportion) =>
            $"{(proportion * 100).ToString("0.0", CultureInfo.InvariantCulture)}%";

        private sealed record ResolvedQuorumTarget(SocketGuildChannel Channel, QuorumSettingsType TargetType);
    }
}