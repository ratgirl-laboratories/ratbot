using System.Text;
using Discord.Interactions;
using RatBot.Domain.Entities;
using RatBot.Infrastructure.Services;

namespace RatBot.Interactions;

[Group("virtue", "Virtue commands.")]
public sealed class VirtueModule(UserVirtueService userVirtueService) : SlashCommandBase
{
    private static readonly string[] MeAdmonitions =
    [
        "Self-knowledge is permitted; self-regard is not.",
        "The wise inspect their standing without becoming attached to it.",
        "Virtue observed too eagerly has already been diminished.",
        "One trusts this inquiry was philosophical, not vain.",
        "Be mindful that the score is a judgement, not an ornament.",
        "The desire to know one's rank is not itself a high-ranking impulse.",
        "Inspection is allowed. Conceit is noted.",
        "It is better to possess virtue than to check on it.",
        "The truly sound citizen seldom refreshes their moral condition.",
        "This curiosity does not, in itself, improve your standing.",
        "A useful reminder: the leaderboard is not a mirror.",
        "Public worth need not be privately fussed over.",
        "Anxiety concerning virtue is rarely a sign of it.",
        "One hopes this was undertaken in humility.",
        "To seek one's score too often is to misunderstand the exercise.",
        "The Server has taken note of your interest in yourself.",
        "Your standing has been revealed; your motives remain under review.",
        "Virtue is best embodied, not monitored.",
        "The impulse to verify oneself is deeply modern and regrettable.",
        "Check less. Participate better.",
        "The score is clearer than the soul that seeks it.",
        "This knowledge was granted, not owed.",
        "The disciplined citizen does not hover over their own esteem.",
        "You have been informed. Try not to make too much of it.",
        "To look upon one's virtue is a privilege best exercised sparingly.",
    ];

    private static readonly string[] LeaderboardAdmonitions =
    [
        "To study the order of others is not always a sign of wisdom.",
        "Comparison is the most vulgar form of civic curiosity.",
        "The leaderboard is a table of virtue, not a spectator sport.",
        "Attend less to those above and below you, and more to your own conduct.",
        "Ranking others is tolerated; envying them is not.",
        "One hopes this inspection was undertaken in a public spirit, not a petty one.",
        "The serious citizen consults the order without resenting it.",
        "Esteem observed too hungrily becomes appetite.",
        "The public hierarchy is not provided for your private agitation.",
        "You have viewed the order of the Server. Compose yourself accordingly.",
        "The wise do not stare upward in envy nor downward in contempt.",
        "Another's standing is not an injury to your own.",
        "The leaderboard reveals much, though rarely what the viewer intended.",
        "To measure the commonwealth is easier than to improve it.",
        "This survey of others will not, by itself, elevate you.",
        "Take care that civic interest does not decay into ordinary nosiness.",
        "The order of virtue is not a horse race.",
        "You have looked upon the public scale. Try not to become theatrical about it.",
        "There is little dignity in treating rank as gossip.",
        "Observe the hierarchy if you must; worship it less.",
        "Public standing is best read with humility and poor impulse control kept to a minimum.",
        "To know who stands above you is useful; to brood on it is not.",
        "The Server has permitted this comparison, not endorsed your motives.",
        "Let this inspection produce emulation rather than bitterness.",
        "Remember: the truly virtuous rarely refresh the leaderboard.",
    ];

    [SlashCommand("me", "Show your current virtue.")]
    public async Task MeAsync()
    {
        if (!await TryDeferEphemeralAsync())
            return;

        int updatedVirtue = await userVirtueService.AddVirtueDeltaAsync(Context.User.Id, -3);
        string admonition = MeAdmonitions[Random.Shared.Next(MeAdmonitions.Length)];

        await SendEphemeralAsync($"{admonition} Your virtue is {updatedVirtue}.");
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

        await userVirtueService.AddVirtueDeltaAsync(Context.User.Id, -5);
        string admonition = LeaderboardAdmonitions[Random.Shared.Next(LeaderboardAdmonitions.Length)];

        List<UserVirtue> topUsers = await userVirtueService.GetTopVirtuesAsync(20);
        if (topUsers.Count == 0)
        {
            await SendEphemeralAsync("No virtue entries found yet.");
            return;
        }

        StringBuilder text = new StringBuilder();
        text.AppendLine(admonition);
        text.AppendLine();
        text.AppendLine("Virtue leaderboard:");
        int rank = 1;

        foreach (UserVirtue entry in topUsers)
        {
            string mention = $"<@{entry.UserId}>";
            text.AppendLine($"{rank}. {mention}: {entry.Virtue}");
            rank++;
        }

        await SendEphemeralAsync(text.ToString());
    }
}
