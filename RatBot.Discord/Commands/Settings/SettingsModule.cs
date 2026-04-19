using System.Collections.Immutable;
using System.Globalization;
using RatBot.Application.Common.Extensions;
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
    public sealed class QuorumSettingsModule(
        IQuorumSettingsWriter quorumSettingsWriter,
        IQuorumSettingsReader quorumSettingsReader,
        IQuorumCommandInputResolver inputResolver)
        : SlashCommandBase
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
            ErrorOr<QuorumSettingsUpsertResult> upsertResult = await (
                from resolvedTarget in inputResolver.ResolveTarget(Context.Guild, target)
                from parsedRoles in inputResolver.ResolveRoles(Context.Guild, roles)
                let parsedRoleIds = parsedRoles.Select(role => role.Id)
                from upsert in quorumSettingsWriter.UpsertAsync(
                    resolvedTarget,
                    parsedRoleIds,
                    proportion)
                select upsert
            );

            await upsertResult.SwitchFirstAsync(
                async result =>
                {
                    ImmutableArray<SocketRole> savedRoles = result.Config
                        .Roles.Select(role => Context.Guild.GetRole(role.Id))
                        .Where(role => role is not null)
                        .ToImmutableArray();

                    await RespondAsync(
                        BuildSetResponse(target, savedRoles, result.Created, proportion),
                        ephemeral: true);
                },
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

            ErrorOr<QuorumTarget> resolvedTargetResult = inputResolver.ResolveTarget(Context.Guild, target);

            if (resolvedTargetResult.IsError)
            {
                await RespondAsync(resolvedTargetResult.FirstError.Description, ephemeral: true);
                return;
            }

            QuorumTarget resolvedTarget = resolvedTargetResult.Value;
            SocketGuildChannel targetChannel = (SocketGuildChannel)target;

            ErrorOr<Deleted> deleteResult = await quorumSettingsWriter.DeleteAsync(resolvedTarget);

            await deleteResult.SwitchFirstAsync(
                async _ => await RespondAsync(BuildUnsetResponse(targetChannel), ephemeral: true),
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
            ErrorOr<QuorumSettings> getResult =
                await (
                    from resolvedTarget in Task.FromResult(inputResolver.ResolveTarget(Context.Guild, target))
                    from settings in quorumSettingsReader.GetAsync(resolvedTarget)
                    select settings
                );

            await getResult.SwitchFirstAsync(
                async settings =>
                {
                    ImmutableArray<SocketRole> resolvedRoles = settings
                        .Roles.Select(role => Context.Guild.GetRole(role.Id))
                        .Where(role => role is not null)
                        .ToImmutableArray();

                    await RespondAsync(
                        BuildViewResponse(target, resolvedRoles, settings.Proportion),
                        ephemeral: true);
                },
                async error => await RespondAsync(DescribeError(error), ephemeral: true)
            );
        }

        private static string BuildSetResponse(
            IChannel channel,
            ImmutableArray<SocketRole> roles,
            bool created,
            double prop)
        {
            string action = created
                ? "created"
                : "updated";

            string renderedRoles = string.Join(", ", roles.Select(role => role.Mention));
            string renderedProportion = FormatProportion(prop);

            return channel switch
            {
                SocketTextChannel textChannel =>
                    $"Quorum settings {action} for channel {textChannel.Mention} with roles {renderedRoles} and proportion {renderedProportion}.",
                SocketCategoryChannel categoryChannel =>
                    $"Quorum settings {action} for category \"{categoryChannel.Name}\" with roles {renderedRoles} and proportion {renderedProportion}.",
                _ => "Invalid channel type for quorum settings."
            };
        }

        private static string BuildViewResponse(IChannel channel, ImmutableArray<SocketRole> roles, double prop)
        {
            string renderedRoles = roles.Length == 0
                ? "none"
                : string.Join(", ", roles.Select(role => role.Mention));

            string renderedProportion = FormatProportion(prop);

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

        private static string FormatProportion(double proportion) =>
            $"{(proportion * 100).ToString("0.0", CultureInfo.InvariantCulture)}%";
    }
}