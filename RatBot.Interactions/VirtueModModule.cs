using Discord;
using Discord.Interactions;
using RatBot.Infrastructure.Services;

namespace RatBot.Interactions;

[Group("virtue-mod", "Virtue moderation commands.")]
[DefaultMemberPermissions(GuildPermission.BanMembers)]
public sealed class VirtueModModule(UserVirtueService userVirtueService) : SlashCommandBase
{
    [SlashCommand("user-virtue", "Get a user's tracked virtue.")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public async Task UserVirtueAsync(IUser user)
    {
        if (!await TryDeferEphemeralAsync())
            return;

        int? virtue = await userVirtueService.TryGetVirtueAsync(user.Id);
        if (virtue is null)
        {
            await SendEphemeralAsync($"{user.Mention} does not have a recorded virtue yet.");
            return;
        }

        await SendEphemeralAsync($"{user.Mention} has a virtue of `{virtue.Value}`.");
    }
}
