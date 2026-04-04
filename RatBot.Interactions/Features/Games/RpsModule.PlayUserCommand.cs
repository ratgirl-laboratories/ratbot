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
    public async Task ChallengeAsync(SocketGuildUser opponent)
    {
        if (Context.User.Id == opponent.Id)
        {
            await RespondAsync("You cannot challenge yourself.", ephemeral: true);
            return;
        }

        if (opponent.IsBot)
        {
            await RespondAsync("You cannot challenge a bot.", ephemeral: true);
            return;
        }

        if (Context.Channel is not ITextChannel)
        {
            await RespondAsync("This command can only be used in a guild text channel.", ephemeral: true);
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

        await RespondAsync(
            $"{Context.User.Mention} challenged {opponent.Mention} to Rock-Paper-Scissors.\nBoth players choose using the buttons below.",
            components: buttons
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
        PurgeExpiredGames();

        if (!Games.TryGetValue(gameId, out RpsGameState? state))
        {
            await RespondAsync("That game is no longer active.", ephemeral: true);
            return;
        }

        if (!TryParsePick(pickRaw, out RpsPick pick))
        {
            await RespondAsync("Invalid pick.", ephemeral: true);
            return;
        }

        bool isChallenger = Context.User.Id == state.ChallengerId;
        bool isOpponent = Context.User.Id == state.OpponentId;

        if (!isChallenger && !isOpponent)
        {
            await RespondAsync("You are not part of this game.", ephemeral: true);
            return;
        }

        state = isChallenger ? state with { ChallengerPick = pick } : state with { OpponentPick = pick };
        Games[gameId] = state;

        await RespondAsync($"Locked in: **{pick}**.", ephemeral: true);

        if (state.ChallengerPick is null || state.OpponentPick is null)
            return;

        Games.TryRemove(gameId, out _);

        string result = GetResultText(state.ChallengerPick.Value, state.OpponentPick.Value);
        string summary =
            $"Game complete: <@{state.ChallengerId}> picked **{state.ChallengerPick}**, <@{state.OpponentId}> picked **{state.OpponentPick}**.\n{result}";

        await FollowupAsync(summary);
    }
}
