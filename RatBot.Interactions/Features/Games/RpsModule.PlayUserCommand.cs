using System.Diagnostics;

namespace RatBot.Interactions.Features.Games;

public sealed partial class RpsModule
{
    private const string DiagEventName = "interaction_diagnostics";
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
    // ReSharper disable once UnusedMember.Global
    public async Task ChallengeAsync(IUser opponent)
    {
        Stopwatch totalStopwatch = Stopwatch.StartNew();
        ILogger timingLogger = CreateTimingLogger("challenge");
        timingLogger.Information(
            "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} total_ms={total_ms} has_responded={has_responded}",
            "challenge_start",
            "start",
            0,
            Context.Interaction.HasResponded
        );

        Stopwatch deferStopwatch = Stopwatch.StartNew();
        if (!await TryDeferPublicAsync())
        {
            timingLogger.Warning(
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} defer_ms={defer_ms} total_ms={total_ms}",
                "challenge_defer",
                "failed",
                Math.Round(deferStopwatch.Elapsed.TotalMilliseconds, 2),
                Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
            );

            return;
        }

        timingLogger.Information(
            "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} defer_ms={defer_ms} total_ms={total_ms}",
            "challenge_defer",
            "succeeded",
            Math.Round(deferStopwatch.Elapsed.TotalMilliseconds, 2),
            Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
        );

        if (Context.User.Id == opponent.Id)
        {
            await SendEphemeralAsync("You cannot challenge yourself.");
            timingLogger.Information(
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} total_ms={total_ms}",
                "challenge_validate",
                "rejected_self",
                Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
            );

            return;
        }

        if (opponent.IsBot)
        {
            await SendEphemeralAsync("You cannot challenge a bot.");
            timingLogger.Information(
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} total_ms={total_ms}",
                "challenge_validate",
                "rejected_bot",
                Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
            );

            return;
        }

        if (Context.Channel is not ITextChannel)
        {
            await SendEphemeralAsync("This command can only be used in a guild text channel.");
            timingLogger.Information(
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} total_ms={total_ms}",
                "challenge_validate",
                "rejected_channel_type",
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
            "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} followup_ms={followup_ms} total_ms={total_ms}",
            "challenge_followup",
            "sent",
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
        timingLogger.Information(
            "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} total_ms={total_ms} has_responded={has_responded}",
            "choose_start",
            "start",
            0,
            Context.Interaction.HasResponded
        );

        Stopwatch deferStopwatch = Stopwatch.StartNew();
        if (!await TryDeferEphemeralAsync())
        {
            timingLogger.Warning(
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} defer_ms={defer_ms} total_ms={total_ms}",
                "choose_defer",
                "failed",
                Math.Round(deferStopwatch.Elapsed.TotalMilliseconds, 2),
                Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
            );

            return;
        }

        timingLogger.Information(
            "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} defer_ms={defer_ms} total_ms={total_ms}",
            "choose_defer",
            "succeeded",
            Math.Round(deferStopwatch.Elapsed.TotalMilliseconds, 2),
            Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
        );

        PurgeExpiredGames();

        if (!Games.TryGetValue(gameId, out RpsGameState? state))
        {
            await SendEphemeralAsync("That game is no longer active.");
            timingLogger.Information(
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} total_ms={total_ms}",
                "choose_validate",
                "game_not_found",
                Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
            );

            return;
        }

        if (!TryParsePick(pickRaw, out RpsPick pick))
        {
            await SendEphemeralAsync("Invalid pick.");
            timingLogger.Information(
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} total_ms={total_ms}",
                "choose_validate",
                "invalid_pick",
                Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
            );

            return;
        }

        bool isChallenger = Context.User.Id == state.ChallengerId;
        bool isOpponent = Context.User.Id == state.OpponentId;

        if (!isChallenger && !isOpponent)
        {
            await SendEphemeralAsync("You are not part of this game.");
            timingLogger.Information(
                "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} total_ms={total_ms}",
                "choose_validate",
                "unauthorized_user",
                Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
            );

            return;
        }

        state = isChallenger ? state with { ChallengerPick = pick } : state with { OpponentPick = pick };
        Games[gameId] = state;

        await SendEphemeralAsync($"Locked in: **{pick}**.");

        timingLogger.Information(
            "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} total_ms={total_ms}",
            "choose_record",
            "pick_recorded",
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
            "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} followup_ms={followup_ms} total_ms={total_ms}",
            "choose_result",
            "sent",
            Math.Round(followupStopwatch.Elapsed.TotalMilliseconds, 2),
            Math.Round(totalStopwatch.Elapsed.TotalMilliseconds, 2)
        );
    }

    private ILogger CreateTimingLogger(string operation)
    {
        return Log.ForContext<RpsModule>()
            .ForContext("diag_event", DiagEventName)
            .ForContext("diag_component", "rps_module")
            .ForContext("rps_operation", operation)
            .ForContext("interaction_id", Context.Interaction.Id)
            .ForContext("interaction_type", Context.Interaction.Type.ToString())
            .ForContext("interaction_age_ms", Math.Round(DateTimeOffset.UtcNow.Subtract(Context.Interaction.CreatedAt).TotalMilliseconds, 2))
            .ForContext("interaction_created_at_utc", Context.Interaction.CreatedAt.UtcDateTime.ToString("O"))
            .ForContext("user_id", Context.User.Id)
            .ForContext("guild_id", Context.Guild?.Id)
            .ForContext("channel_id", Context.Channel?.Id);
    }
}
