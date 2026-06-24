using RatBot.Application.Moderation;
using RatBot.Discord.SecretRole;
using RatBot.Domain.Moderation;

namespace RatBot.Discord.Commands.Moderation;

[UsedImplicitly]
public static class ModerationModule
{
    [Group("smod", "Senior moderation commands.")]
    [DefaultMemberPermissions(GuildPermission.BanMembers)]
    public sealed class SeniorModeration(ILogger logger, IModerationService moderationService, SecretRoleManager secretRoleManager) : SlashCommandBase
    {
        [SlashCommand("autoban", "Register a user to be banned if they join.")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task AutobanAsync([Summary("user-id", "The Discord user ID to autoban.")] string user)
        {
            if (!ulong.TryParse(user, out ulong parsedUserId) || parsedUserId == 0)
            {
                await RespondAsync("Enter a valid Discord user ID or mention.", ephemeral: true);
                return;
            }

            if (Context.Guild.GetUser(parsedUserId) is not null)
            {
                await RespondAsync("That user is currently in the server. Use a regular ban instead.", ephemeral: true);

                return;
            }

            ulong guildId = Context.Guild.Id;
            ulong moderator = Context.User.Id;

            ErrorOr<AutobannedUser> result = await moderationService.RegisterAutobanAsync(guildId, parsedUserId, moderator);

            if (result.IsError)
            {
                await RespondAsync(result.FirstError.Description, ephemeral: true);
                return;
            }

            logger.Information(
                "User {User} ({UserId}) registered autoban for {TargetId} in guild {GuildId}.",
                Context.User.Username,
                Context.User.Id,
                parsedUserId,
                guildId
            );

            await RespondAsync($"Registered <@{parsedUserId}> for autoban.", ephemeral: true);
        }

        [SlashCommand("set-ping-role", "Set the role users receive when they mention it.")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task SetPingRoleAsync([Summary("role", "A role ID or role mention.")] string role)
        {
            if (!TryParseRoleId(role, out ulong roleId))
            {
                await RespondAsync("Enter a valid role ID or role mention.", ephemeral: true);
                return;
            }

            SocketRole? guildRole = Context.Guild.GetRole(roleId);

            if (guildRole is null)
            {
                await RespondAsync("That role does not exist in this server.", ephemeral: true);
                return;
            }

            SocketGuildUser botUser = Context.Guild.CurrentUser;
            bool isAssignable =
                guildRole.Id != Context.Guild.EveryoneRole.Id
                && !guildRole.IsManaged
                && botUser.GuildPermissions.ManageRoles
                && guildRole.Position < botUser.Hierarchy;

            if (!isAssignable)
            {
                await RespondAsync("I cannot assign that role. Choose a normal role below my highest role.", ephemeral: true);
                return;
            }

            await secretRoleManager.ReplaceAsync(Context.Guild.Id, guildRole.Id).ConfigureAwait(false);

            logger.Information(
                "User {UserId} configured temporary ping role {RoleId} in guild {GuildId}.",
                Context.User.Id,
                guildRole.Id,
                Context.Guild.Id
            );

            await RespondAsync($"Set <@&{guildRole.Id}> as the temporary ping role.", ephemeral: true);
        }

        private static bool TryParseRoleId(string value, out ulong roleId)
        {
            string candidate = value.Trim();

            if (candidate.StartsWith("<@&", StringComparison.Ordinal) && candidate.EndsWith('>'))
                candidate = candidate[3..^1];

            return ulong.TryParse(candidate, out roleId) && roleId != 0;
        }
    }

    [Group("mod", "Moderation commands.")]
    [DefaultMemberPermissions(GuildPermission.MuteMembers)]
    public sealed class Moderation : SlashCommandBase { }
}
