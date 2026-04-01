using System.Text;
using Discord.Interactions;
using RatBot.Domain.Entities;
using RatBot.Infrastructure.Services;

namespace RatBot.Interactions;

[Group("virtue", "Virtue commands.")]
public sealed class VirtueModule(UserVirtueService userVirtueService) : SlashCommandBase
{
    [SlashCommand("me", "Show your current virtue.")]
    public async Task MeAsync()
    {
        if (!await TryDeferEphemeralAsync())
            return;

        int updatedVirtue = await userVirtueService.GetVirtueAsync(Context.User.Id);

        await SendEphemeralAsync($"Your virtue is {updatedVirtue}.");
    }

    [SlashCommand("leaderboard", "Show the top 20 users by virtue.")]
    public async Task LeaderboardAsync()
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command can only be used in a guild.", ephemeral: true);
            return;
        }

        if (!await TryDeferEphemeralAsync())
            return;

        List<UserVirtue> topUsers = await userVirtueService.GetTopVirtuesAsync(20);
        if (topUsers.Count == 0)
        {
            await SendEphemeralAsync("No virtue entries found yet.");
            return;
        }

        StringBuilder text = new StringBuilder("Virtue leaderboard:\n");
        int rank = 1;

        foreach (UserVirtue entry in topUsers)
        {
            string mention = $"<@{entry.UserId}>";
            text.AppendLine($"{rank}. {mention}: {entry.Virtue}");
            rank++;
        }

        await SendEphemeralAsync(text.ToString());
    }

    [SlashCommand("behold", "Behold the lowest amongst thee")]
    public async Task BeholdAsync()
    {
        if (Context.Guild is null)
        {
            await RespondAsync("This command can only be used in a guild.", ephemeral: true);
            return;
        }

        UserVirtue? lowestVirtue = await userVirtueService.GetLowestVirtueAsync();
        if (lowestVirtue is null)
        {
            await RespondAsync("No virtue entries found yet.");
            return;
        }

        await RespondAsync(
            $"Behold the lowest amongst thee: <@{lowestVirtue.UserId}> with virtue `{lowestVirtue.Virtue}`."
        );
    }
}
