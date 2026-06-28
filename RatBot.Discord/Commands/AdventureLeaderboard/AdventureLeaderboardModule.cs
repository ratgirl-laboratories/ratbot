using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using RatBot.Infrastructure.Data;

namespace RatBot.Discord.Commands.AdventureLeaderboard;

[Group("adventure", "Practical Python Adventure commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class AdventureLeaderboardModule(AdventureLeaderboardManager manager, BotDbContext dbContext)
    : InteractionModuleBase<IInteractionContext>
{
    [SlashCommand("create-threads", "Create private adventure solution threads under a text channel.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task CreateThreadsAsync([Summary("channel_id", "Parent text channel ID.")] string channelId)
    {
        if (!ulong.TryParse(channelId, out ulong parsedChannelId))
        {
            await RespondAsync("Channel ID must be a Discord snowflake.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        ITextChannel? channel = await Context.Guild.GetTextChannelAsync(parsedChannelId).ConfigureAwait(false);

        if (channel is null)
        {
            await RespondAsync("That channel ID does not resolve to a text channel in this guild.", ephemeral: true).ConfigureAwait(false);

            return;
        }

        bool alreadyConfigured = await dbContext.AdventureForumThreadLinks.AnyAsync().ConfigureAwait(false);

        if (alreadyConfigured)
        {
            await RespondAsync(
                    "Adventure forum solution threads are already configured. Remove the existing thread mappings before creating a replacement set.",
                    ephemeral: true
                )
                .ConfigureAwait(false);

            return;
        }

        await DeferAsync(ephemeral: true).ConfigureAwait(false);

        ImmutableArray<AdventureThreadLinkage> plans = AdventureScorePart
            .All.Select(scorePart => new AdventureThreadLinkage(scorePart.Index, scorePart.ThreadName))
            .ToImmutableArray();

        List<AdventureForumThreadLink> links = new List<AdventureForumThreadLink>(plans.Length);

        try
        {
            foreach (AdventureThreadLinkage plan in plans)
            {
                IThreadChannel thread = await channel
                    .CreateThreadAsync(plan.ThreadName, ThreadType.PrivateThread, ThreadArchiveDuration.OneWeek, invitable: false)
                    .ConfigureAwait(false);

                links.Add(AdventureForumThreadLink.Create(plan.ScorePartIndex, thread.Id));
            }

            dbContext.AdventureForumThreadLinks.AddRange(links);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            await FollowupAsync(
                    "I could not create all adventure solution threads or save their mappings. Some threads may have been created; check the channel before retrying.",
                    ephemeral: true
                )
                .ConfigureAwait(false);

            throw;
        }

        await FollowupAsync($"Created {links.Count} private adventure solution threads in {channel.Mention}.", ephemeral: true).ConfigureAwait(false);
    }

    [SlashCommand("exclude", "Exclude a user from the active adventure leaderboard.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ExcludeAsync([Summary("user", "User to exclude from the adventure leaderboard.")] IUser user)
    {
        await DeferAsync(ephemeral: true).ConfigureAwait(false);

        bool added = await manager.ExcludeUserAsync(user.Id, CancellationToken.None).ConfigureAwait(false);
        string displayName = Format.Sanitize(user.Username);
        string action = added ? "Excluded" : "Already excluding";

        await FollowupAsync($"{action} {displayName} ({user.Id}) from the adventure leaderboard.", ephemeral: true).ConfigureAwait(false);
    }

    [SlashCommand("show-leaderboard", "Create and keep an adventure leaderboard message updated.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ShowLeaderboardAsync(
        [Summary("channel", "Channel where RatBot should post the leaderboard.")] ITextChannel channel,
        [Summary("year", "Adventure event year to show.")] int year
    )
    {
        IGuildUser currentUser = await Context.Guild.GetCurrentUserAsync().ConfigureAwait(false);
        ChannelPermissions permissions = currentUser.GetPermissions(channel);

        if (!permissions.ViewChannel || !permissions.SendMessages)
        {
            await RespondAsync("I cannot send messages in that channel.", ephemeral: true).ConfigureAwait(false);
            return;
        }

        await DeferAsync(ephemeral: true).ConfigureAwait(false);

        try
        {
            IUserMessage message = await manager.CreateLeaderboardMessageAsync(channel, year, CancellationToken.None).ConfigureAwait(false);

            await FollowupAsync($"Leaderboard posted in {channel.Mention}: {message.GetJumpUrl()}", ephemeral: true).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await FollowupAsync($"I could not create the leaderboard message in {channel.Mention}.", ephemeral: true).ConfigureAwait(false);

            throw;
        }
    }
}
