using System.Diagnostics;

namespace RatBot.Interactions.Features.Games;

public sealed partial class RpsModule
{
    private const string CustomIdPrefix = "rps";

    private static string GetCustomId(string gameId, RpsPick pick) => $"{CustomIdPrefix}:{gameId}:{pick.ToString().ToLowerInvariant()}";

    private static bool TryParsePick(string value, out RpsPick pick)
    {
        switch (value.ToLowerInvariant())
        {
            case "rock":
                pick = RpsPick.Rock;
                return true;
            case "paper":
                pick = RpsPick.Paper;
                return true;
            case "scissors":
                pick = RpsPick.Scissors;
                return true;
            default:
                pick = default;
                return false;
        }
    }

    private static string GetResultText(RpsPick challengerPick, RpsPick opponentPick)
    {
        if (challengerPick == opponentPick)
            return "It's a tie.";

        bool challengerWon =
            (challengerPick == RpsPick.Rock && opponentPick == RpsPick.Scissors)
            || (challengerPick == RpsPick.Paper && opponentPick == RpsPick.Rock)
            || (challengerPick == RpsPick.Scissors && opponentPick == RpsPick.Paper);

        return challengerWon ? "Challenger wins." : "Opponent wins.";
    }

    /// <summary>
    /// Starts a rock-paper-scissors game against the selected user.
    /// </summary>
    /// <param name="opponent">The challenged user.</param>
    [UserCommand("Challenge to RPS")]
    public async Task ChallengeAsync(IUser opponent)
    {
        Stopwatch totalStopwatch = Stopwatch.StartNew();
        ILogger timingLogger = CreateTimingLogger("challenge");
        timingLogger.Information("rps_timing challenge_start. HasRespondedAtEntry={HasRespondedAtEntry}", Context.Interaction.HasResponded);

        Stopwatch deferStopwatch = Stopwatch.StartNew();
        if (!await TryDeferPublicAsync())
        {
            timingLogger.Warning(
                "rps_timing challenge_defer_failed. DeferMs={DeferMs} TotalMs={TotalMs}",
                Math.Round(deferStopwatch.Elapsed.TotalMilliseconds, 2),
                Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
            );
            return;
        }

        timingLogger.Information(
            "rps_timing challenge_defer_succeeded. DeferMs={DeferMs} TotalMs={TotalMs}",
            Math.Round(deferStopwatch.Elapsed.TotalMilliseconds, 2),
            Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
        );

        if (Context.User.Id == opponent.Id)
        {
            await SendEphemeralAsync("You cannot challenge yourself.");
            timingLogger.Information("rps_timing challenge_rejected_self. TotalMs={TotalMs}", Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2));
            return;
        }

        if (opponent.IsBot)
        {
            await SendEphemeralAsync("You cannot challenge a bot.");
            timingLogger.Information("rps_timing challenge_rejected_bot. TotalMs={TotalMs}", Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2));
            return;
        }

        if (Context.Channel is not ITextChannel)
        {
            await SendEphemeralAsync("This command can only be used in a guild text channel.");
            timingLogger.Information(
                "rps_timing challenge_rejected_channel_type. TotalMs={TotalMs}",
                Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
            );
            return;
        }

        PurgeExpiredGames();

        string gameId = Guid.NewGuid().ToString("N");
        Games[gameId] = new RpsGameState(Context.User.Id, opponent.Id, DateTimeOffset.UtcNow, null, null);

        MessageComponent buttons = new ComponentBuilder()
            .WithButton("Rock", GetCustomId(gameId, RpsPick.Rock))
            .WithButton("Paper", GetCustomId(gameId, RpsPick.Paper))
            .WithButton("Scissors", GetCustomId(gameId, RpsPick.Scissors))
            .Build();

        Stopwatch followupStopwatch = Stopwatch.StartNew();
        await FollowupAsync(
            $"{Context.User.Mention} challenged {opponent.Mention} to Rock-Paper-Scissors.\nBoth players choose using the buttons below.",
            components: buttons
        );

        timingLogger.Information(
            "rps_timing challenge_followup_sent. FollowupMs={FollowupMs} TotalMs={TotalMs}",
            Math.Round(followupStopwatch.Elapsed.TotalMilliseconds, 2),
            Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
        );
    }

    /// <summary>
    /// Handles a player's rock/paper/scissors selection.
    /// </summary>
    /// <param name="gameId">The game identifier.</param>
    /// <param name="pickRaw">The selected pick value.</param>
    [ComponentInteraction($"{CustomIdPrefix}:*:*", ignoreGroupNames: true)]
    public async Task ChooseAsync(string gameId, string pickRaw)
    {
        Stopwatch totalStopwatch = Stopwatch.StartNew();
        ILogger timingLogger = CreateTimingLogger("choose");
        timingLogger.Information("rps_timing choose_start. HasRespondedAtEntry={HasRespondedAtEntry}", Context.Interaction.HasResponded);

        Stopwatch deferStopwatch = Stopwatch.StartNew();
        if (!await TryDeferEphemeralAsync())
        {
            timingLogger.Warning(
                "rps_timing choose_defer_failed. DeferMs={DeferMs} TotalMs={TotalMs}",
                Math.Round(deferStopwatch.Elapsed.TotalMilliseconds, 2),
                Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
            );
            return;
        }

        timingLogger.Information(
            "rps_timing choose_defer_succeeded. DeferMs={DeferMs} TotalMs={TotalMs}",
            Math.Round(deferStopwatch.Elapsed.TotalMilliseconds, 2),
            Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
        );

        PurgeExpiredGames();

        if (!Games.TryGetValue(gameId, out RpsGameState? state))
        {
            await SendEphemeralAsync("That game is no longer active.");
            timingLogger.Information("rps_timing choose_game_not_found. TotalMs={TotalMs}", Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2));
            return;
        }

        if (!TryParsePick(pickRaw, out RpsPick pick))
        {
            await SendEphemeralAsync("Invalid pick.");
            timingLogger.Information("rps_timing choose_invalid_pick. TotalMs={TotalMs}", Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2));
            return;
        }

        bool isChallenger = Context.User.Id == state.ChallengerId;
        bool isOpponent = Context.User.Id == state.OpponentId;

        if (!isChallenger && !isOpponent)
        {
            await SendEphemeralAsync("You are not part of this game.");
            timingLogger.Information(
                "rps_timing choose_unauthorized_user. TotalMs={TotalMs}",
                Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
            );
            return;
        }

        state = isChallenger ? state with { ChallengerPick = pick } : state with { OpponentPick = pick };
        Games[gameId] = state;

        await SendEphemeralAsync($"Locked in: **{pick}**.");

        timingLogger.Information(
            "rps_timing choose_pick_recorded. TotalMs={TotalMs}",
            Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
        );

        if (state.ChallengerPick is null || state.OpponentPick is null)
            return;

        Games.TryRemove(gameId, out _);

        string result = GetResultText(state.ChallengerPick.Value, state.OpponentPick.Value);
        string summary =
            $"Game complete: <@{state.ChallengerId}> picked **{state.ChallengerPick}**, <@{state.OpponentId}> picked **{state.OpponentPick}**.\n{result}";

        Stopwatch followupStopwatch = Stopwatch.StartNew();
        await FollowupAsync(summary);

        timingLogger.Information(
            "rps_timing choose_result_sent. FollowupMs={FollowupMs} TotalMs={TotalMs}",
            Math.Round(followupStopwatch.Elapsed.TotalMilliseconds, 2),
            Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
        );
    }

    private ILogger CreateTimingLogger(string operation)
    {
        return Log.ForContext<RpsModule>()
            .ForContext("RpsOperation", operation)
            .ForContext("InteractionId", Context.Interaction.Id)
            .ForContext("InteractionType", Context.Interaction.Type.ToString())
            .ForContext("InteractionAgeMsAtEntry", Math.Round(DateTimeOffset.UtcNow.Subtract(Context.Interaction.CreatedAt).TotalMilliseconds, 2))
            .ForContext("UserId", Context.User.Id)
            .ForContext("GuildId", Context.Guild?.Id)
            .ForContext("ChannelId", Context.Channel?.Id);
    }
}
