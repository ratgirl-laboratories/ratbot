using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RatBot.Application.Common.Extensions;
using RatBot.Application.Meta;
using RatBot.Discord.BackgroundWorkers;
using RatBot.Infrastructure.Data;
using RatBot.Infrastructure.RoleColours;

namespace RatBot.Discord.Commands.Settings;

[Group("config", "Configuration commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class SettingsModule : SlashCommandBase
{
    // ReSharper disable InconsistentNaming
    private const string RESPONSE_NO_GUILD = "This command can only be used in a guild.";
    private const string LABEL_REFRESH = "Refresh";

    // ReSharper enable InconsistentNaming

    [Group("colour", "Role colour configuration.")]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    public sealed class ColourSettingsModule(BotDbContext dbContext, IRoleColourSyncQueue syncQueue) : SlashCommandBase
    {
        [SlashCommand("add", "Register a source/display role colour mapping.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task AddAsync(
            [Summary("name", "Name used to identify this colour.")]
            string name,
            [Summary("source", "Source colour role users select.")]
            IRole source,
            [Summary("display", "Display colour role RatBot manages.")]
            IRole display
        )
        {
            if (Context.Guild is null)
            {
                await RespondAsync(RESPONSE_NO_GUILD, ephemeral: true);
                return;
            }

            ErrorOr<Success> roleValidation = ValidateRoleColourRoles(source.Id, display.Id);

            if (roleValidation.IsError)
            {
                await RespondAsync(roleValidation.FirstError.Description, ephemeral: true);
                return;
            }

            await DeferAsync(ephemeral: true);

            ErrorOr<RoleColourOption> result = await RegisterRoleColourOption.ExecuteAsync(
                dbContext,
                name,
                name,
                source.Id,
                display.Id,
                CancellationToken.None
            );

            await result.SwitchFirstAsync(
                async option =>
                {
                    int queued = await EnqueueMembersWithSourceRoleAsync(Context.Guild, option.SourceRoleId);

                    IRoleColourSyncQueue.Status status = syncQueue.GetStatus();
                    string eta = FormatEta(status);

                    string message =
                        $"Registered colour option `{option.Key}` (‘{option.Label}’): {source.Mention} -> {display.Mention}.\n"
                        + $"Queued {queued} member(s) for colour sync. Current queue: pending={status.Pending}, in_flight={status.InFlight}, ETA={eta}.";

                    ComponentBuilder components =
                        (status.Pending + status.InFlight) == 0
                            ? new ComponentBuilder()
                            : new ComponentBuilder().WithButton(
                                label: LABEL_REFRESH,
                                customId: $"colour-sync-refresh:{Context.User.Id}",
                                style: ButtonStyle.Primary
                            );

                    await FollowupAsync(message, ephemeral: true, components: components.Build());
                },
                async error => await FollowupAsync(error.Description, ephemeral: true)
            );
        }

        [SlashCommand("upsert", "Create or update a source/display role colour mapping.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task UpsertAsync(
            [Summary("name", "Name used to identify this colour.")]
            string name,
            [Summary("source", "Source colour role users select.")]
            IRole source,
            [Summary("display", "Display colour role RatBot manages.")]
            IRole display
        )
        {
            if (Context.Guild is null)
            {
                await RespondAsync(RESPONSE_NO_GUILD, ephemeral: true);
                return;
            }

            ErrorOr<Success> roleValidation = ValidateRoleColourRoles(source.Id, display.Id);

            if (roleValidation.IsError)
            {
                await RespondAsync(roleValidation.FirstError.Description, ephemeral: true);
                return;
            }

            await DeferAsync(ephemeral: true);

            ErrorOr<UpsertRoleColourOption.Result> result = await UpsertRoleColourOption.ExecuteAsync(
                dbContext,
                name,
                name,
                source.Id,
                display.Id,
                CancellationToken.None
            );

            await result.SwitchFirstAsync(
                async upsert =>
                {
                    HashSet<ulong> affectedRoleIds = new HashSet<ulong>
                        { upsert.Option.SourceRoleId, upsert.Option.DisplayRoleId };

                    if (upsert.PreviousDisplayRoleId is not null)
                        affectedRoleIds.Add(upsert.PreviousDisplayRoleId.Value);

                    int queued = await EnqueueMembersWithAnyOfRolesAsync(Context.Guild, affectedRoleIds);

                    IRoleColourSyncQueue.Status status = syncQueue.GetStatus();
                    string eta = FormatEta(status);

                    string action = upsert.Created
                        ? "Registered"
                        : "Updated";

                    string message =
                        $"{action} colour option `{upsert.Option.Key}` (‘{upsert.Option.Label}’): {source.Mention} -> {display.Mention}.\n"
                        + $"Queued {queued} member(s) for colour sync. Current queue: pending={status.Pending}, in_flight={status.InFlight}, ETA={eta}.";

                    ComponentBuilder components =
                        (status.Pending + status.InFlight) == 0
                            ? new ComponentBuilder()
                            : new ComponentBuilder().WithButton(
                                label: LABEL_REFRESH,
                                customId: $"colour-sync-refresh:{Context.User.Id}",
                                style: ButtonStyle.Primary
                            );

                    await FollowupAsync(message, ephemeral: true, components: components.Build());
                },
                async error => await FollowupAsync(error.Description, ephemeral: true)
            );
        }

        [SlashCommand("delete", "Delete a configured role colour option.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task DeleteAsync([Summary("name", "Name/key of the colour option to delete.")] string name)
        {
            if (Context.Guild is null)
            {
                await RespondAsync(RESPONSE_NO_GUILD, ephemeral: true);
                return;
            }

            await DeferAsync(ephemeral: true);

            ErrorOr<RoleColourOption> result = await DeleteRoleColourOption.ExecuteAsync(
                dbContext,
                new DeleteRoleColourOption.Command(name),
                CancellationToken.None
            );

            await result.SwitchFirstAsync(
                async option =>
                {
                    // Enqueue members that either had the source role or currently wear the display role
                    int queued = await EnqueueMembersWithAnyOfRolesAsync(
                        Context.Guild,
                        new HashSet<ulong> { option.SourceRoleId, option.DisplayRoleId }
                    );

                    IRoleColourSyncQueue.Status status = syncQueue.GetStatus();
                    string eta = FormatEta(status);

                    ComponentBuilder components =
                        (status.Pending + status.InFlight) == 0
                            ? new ComponentBuilder()
                            : new ComponentBuilder().WithButton(
                                label: LABEL_REFRESH,
                                customId: $"colour-sync-refresh:{Context.User.Id}",
                                style: ButtonStyle.Primary
                            );

                    await FollowupAsync(
                        $"Deleted colour option `{option.Key}`. Queued {queued} member(s) for colour sync. Current queue: pending={status.Pending}, in_flight={status.InFlight}, ETA={eta}.",
                        ephemeral: true,
                        components: components.Build()
                    );
                },
                async error => await FollowupAsync(error.Description, ephemeral: true)
            );
        }

        [SlashCommand("list", "List configured role colour options.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task ListAsync(
            [Summary("include-disabled", "Include disabled colour options.")]
            bool includeDisabled = true)
        {
            if (Context.Guild is null)
            {
                await RespondAsync(RESPONSE_NO_GUILD, ephemeral: true);
                return;
            }

            IReadOnlyList<RoleColourOption> options = await ListRoleColourOptions.ExecuteAsync(
                dbContext,
                new ListRoleColourOptions.Query(includeDisabled),
                CancellationToken.None
            );

            if (options.Count == 0)
            {
                await RespondAsync("No colour options are configured.", ephemeral: true);
                return;
            }

            await RespondAsync(BuildListResponse(options), ephemeral: true);
        }

        [SlashCommand(
            "sync",
            "Reconcile display colour roles for all members who currently have a source colour role.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SyncAsync()
        {
            if (Context.Guild is null)
            {
                await RespondAsync(RESPONSE_NO_GUILD, ephemeral: true);
                return;
            }

            await DeferAsync(ephemeral: true);

            // Load enabled options and gather SCR set
            List<RoleColourOption> enabled = await dbContext
                .RoleColourOptions.AsNoTracking()
                .Where(o => o.IsEnabled)
                .ToListAsync(CancellationToken.None);

            if (enabled.Count == 0)
            {
                await FollowupAsync("No enabled colour options are configured.", ephemeral: true);
                return;
            }

            HashSet<ulong> scrSet = enabled.Select(o => o.SourceRoleId).ToHashSet();

            int queued = await EnqueueMembersWithAnySourceRoleAsync(Context.Guild, scrSet);
            IRoleColourSyncQueue.Status status = syncQueue.GetStatus();
            string eta = FormatEta(status);

            ComponentBuilder components =
                (status.Pending + status.InFlight) == 0
                    ? new ComponentBuilder()
                    : new ComponentBuilder().WithButton(
                        label: LABEL_REFRESH,
                        customId: $"colour-sync-refresh:{Context.User.Id}",
                        style: ButtonStyle.Primary
                    );

            await FollowupAsync(
                $"Queued {queued} member(s). Current queue: pending={status.Pending}, in_flight={status.InFlight}, ETA={eta}.",
                ephemeral: true,
                components: components.Build()
            );
        }

        private async Task<int> EnqueueMembersWithSourceRoleAsync(IGuild guild, ulong sourceRoleId)
        {
            IReadOnlyCollection<IGuildUser> users = await guild.GetUsersAsync();

            ImmutableArray<IGuildUser> targetUsers =
                users.Where(u => u.RoleIds.Contains(sourceRoleId)).ToImmutableArray();

            return targetUsers.Count(user => syncQueue.Enqueue(guild.Id, user.Id));
        }

        private async Task<int> EnqueueMembersWithAnySourceRoleAsync(IGuild guild, HashSet<ulong> sourceRoleIds)
        {
            IReadOnlyCollection<IGuildUser> users = await guild.GetUsersAsync();

            ImmutableArray<IGuildUser> targetUsers =
                users.Where(u => u.RoleIds.Any(sourceRoleIds.Contains)).ToImmutableArray();

            return targetUsers.Count(user => syncQueue.Enqueue(guild.Id, user.Id));
        }

        private async Task<int> EnqueueMembersWithAnyOfRolesAsync(IGuild guild, HashSet<ulong> roleIds)
        {
            IReadOnlyCollection<IGuildUser> users = await guild.GetUsersAsync();

            ImmutableArray<IGuildUser> targetUsers =
                users.Where(u => u.RoleIds.Any(roleIds.Contains)).ToImmutableArray();

            return targetUsers.Count(user => syncQueue.Enqueue(guild.Id, user.Id));
        }

        private static string FormatEta(IRoleColourSyncQueue.Status status)
        {
            if (status.Eta is null)
                return "unknown";

            TimeSpan eta = status.Eta.Value;

            if (eta.TotalSeconds < 1)
                return "<1s";

            if (eta.TotalMinutes < 1)
                return $"~{Math.Ceiling(eta.TotalSeconds)}s";

            return eta.TotalHours < 1
                ? $"~{Math.Floor(eta.TotalMinutes)}m {eta.Seconds:D2}s"
                : $"~{Math.Floor(eta.TotalHours)}h {eta.Minutes:D2}m";
        }

        private ErrorOr<Success> ValidateRoleColourRoles(ulong sourceRoleId, ulong displayRoleId)
        {
            SocketRole? sourceRole = Context.Guild.GetRole(sourceRoleId);
            SocketRole? displayRole = Context.Guild.GetRole(displayRoleId);

            if (sourceRole is null)
                return Error.Validation(description: "Source role does not exist in this guild.");

            if (displayRole is null)
                return Error.Validation(description: "Display role does not exist in this guild.");

            if (sourceRole.Colors.PrimaryColor != Color.Default)
                return Error.Validation(description: "Source role must not have a colour set. Clear the colour first.");

            return Result.Success;
        }

        private static string BuildListResponse(IReadOnlyList<RoleColourOption> options)
        {
            StringBuilder builder = new StringBuilder("Configured colour options:");

            foreach (RoleColourOption option in options)
            {
                string state = option.IsEnabled
                    ? "enabled"
                    : "disabled";

                builder.AppendLine()
                    .Append($"`{option.Key}`: <@&{option.SourceRoleId}> -> <@&{option.DisplayRoleId}> ({state})");
            }

            return builder.ToString();
        }

        [ComponentInteraction("colour-sync-refresh:*", true)]
        public async Task OnColourSyncRefreshAsync(ulong ownerUserId)
        {
            if (Context.User.Id != ownerUserId)
            {
                await RespondAsync("This status panel is not for you.", ephemeral: true);
                return;
            }

            IRoleColourSyncQueue.Status status = syncQueue.GetStatus();
            string eta = FormatEta(status);

            bool done = (status.Pending + status.InFlight) == 0;

            string content = done
                ? "Role colour sync complete. Queue is empty."
                : $"Current queue: pending={status.Pending}, in_flight={status.InFlight}, ETA={eta}.";

            ComponentBuilder components = done
                ? new ComponentBuilder()
                : new ComponentBuilder().WithButton(
                    label: LABEL_REFRESH,
                    customId: $"colour-sync-refresh:{ownerUserId}",
                    style: ButtonStyle.Primary);

            try
            {
                if (Context.Interaction is SocketMessageComponent smc)
                {
                    await smc.UpdateAsync(m =>
                    {
                        m.Content = content;
                        m.Components = components.Build();
                    });
                }
                else
                {
                    await RespondAsync(content, ephemeral: true, components: components.Build());
                }
            }
            catch
            {
                // best-effort; ignore update failures
            }
        }
    }

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
                async error => await RespondAsync(error.Description, ephemeral: true));
        }
    }

    [Group("quorum", "Quorum configuration.")]
    [DefaultMemberPermissions(GuildPermission.Administrator)]
    public sealed class QuorumSettingsModule(
        IQuorumSettingsWriter quorumSettingsWriter,
        IQuorumSettingsReader quorumSettingsReader,
        IQuorumCommandInputResolver inputResolver
    ) : SlashCommandBase
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
                from upsert in quorumSettingsWriter.UpsertAsync(resolvedTarget, parsedRoleIds, proportion)
                select upsert
            );

            await upsertResult.SwitchFirstAsync(
                async result =>
                {
                    ImmutableArray<SocketRole> savedRoles = result
                        .Config.Roles.Select(role => Context.Guild.GetRole(role.Id))
                        .Where(role => role is not null)
                        .ToImmutableArray();

                    await RespondAsync(
                        BuildSetResponse(target, savedRoles, result.Created, proportion),
                        ephemeral: true);
                },
                async error => await RespondAsync(DescribeError(error), ephemeral: true));
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
                await RespondAsync(RESPONSE_NO_GUILD, ephemeral: true);
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
                async error => await RespondAsync(DescribeError(error), ephemeral: true));
        }

        [SlashCommand("view", "View the quorum settings for a channel or category.")]
        public async Task ViewAsync(
            [Summary("target", "The target channel or category.")]
            [ChannelTypes(ChannelType.Text, ChannelType.Category)]
            IChannel target
        )
        {
            ErrorOr<QuorumSettings> getResult = await (
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

                    await RespondAsync(BuildViewResponse(target, resolvedRoles, settings.Proportion), ephemeral: true);
                },
                async error => await RespondAsync(DescribeError(error), ephemeral: true));
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
                _ => "Invalid channel type for quorum settings.",
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
                _ => "Invalid channel type for quorum settings.",
            };
        }

        private static string BuildUnsetResponse(SocketGuildChannel channel) =>
            channel switch
            {
                SocketTextChannel textChannel => $"Quorum settings removed for channel {textChannel.Mention}.",
                SocketCategoryChannel categoryChannel =>
                    $"Quorum settings removed for category \"{categoryChannel.Name}\".",
                _ => "Invalid channel type for quorum settings removal.",
            };

        private static string DescribeError(Error error) =>
            error.Type switch
            {
                ErrorType.NotFound => "No quorum settings exist for that target.",
                _ => error.Description,
            };

        private static string FormatProportion(double proportion) =>
            $"{(proportion * 100).ToString("0.0", CultureInfo.InvariantCulture)}%";
    }
}
