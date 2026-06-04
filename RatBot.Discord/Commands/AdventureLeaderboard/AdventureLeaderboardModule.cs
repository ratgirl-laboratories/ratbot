namespace RatBot.Discord.Commands.AdventureLeaderboard;

[Group("adventure", "Practical Python Adventure commands.")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
public sealed class AdventureLeaderboardModule(AdventureLeaderboardUpdateService updateService)
    : InteractionModuleBase<IInteractionContext>
{
    [SlashCommand("show-leaderboard", "Create and keep an adventure leaderboard message updated.")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ShowLeaderboardAsync(
        [Summary("channel", "Channel where RatBot should post the leaderboard.")] ITextChannel channel,
        [Summary("year", "Adventure event year to show.")] int year)
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
            IUserMessage message = await updateService.CreateLeaderboardMessageAsync(
                channel,
                year,
                CancellationToken.None).ConfigureAwait(false);

            await FollowupAsync(
                $"Leaderboard posted in {channel.Mention}: {message.GetJumpUrl()}",
                ephemeral: true).ConfigureAwait(false);
        }
        catch (Exception)
        {
            await FollowupAsync(
                $"I could not create the leaderboard message in {channel.Mention}.",
                ephemeral: true).ConfigureAwait(false);
            throw;
        }
    }
}
