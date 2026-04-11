using RatBot.Application.Features.Rps;

namespace RatBot.Interactions.Modules.Games;

[UsedImplicitly]
public sealed class RpsModule(RpsGameService rpsGameService) : SlashCommandBase
{
    private const string DiagEventName = "interaction_diagnostics";
    private const string CustomIdPrefix = "rps";

    private static string GetCustomId(string gameId, RpsPick pick) =>
        $"{CustomIdPrefix}:{gameId}:{pick.ToString().ToLowerInvariant()}";

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

    private static string GetResultText(RpsGameOutcome outcome) =>
        outcome switch
        {
            RpsGameOutcome.Tie => "It's a tie.",
            RpsGameOutcome.ChallengerWon => "Challenger wins.",
            RpsGameOutcome.OpponentWon => "Opponent wins.",
            _ => "Game complete."
        };

    [UserCommand("Challenge to RPS")]
    public async Task ChallengeAsync(IUser opponent)
    {
        ILogger timingLogger = CreateTimingLogger("challenge");

        timingLogger.Information(
            "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} total_ms={total_ms} has_responded={has_responded}",
            "challenge_start",
            "start",
            0,
            Context.Interaction.HasResponded);

        if (!await TryDeferPublicAsync())
            return;

        if (Context.User.Id == opponent.Id)
        {
            await SendEphemeralAsync("You cannot challenge yourself.");
            return;
        }

        if (opponent.IsBot)
        {
            await SendEphemeralAsync("You cannot challenge a bot.");
            return;
        }

        if (Context.Channel is not ITextChannel)
        {
            await SendEphemeralAsync("This command can only be used in a guild text channel.");
            return;
        }

        RpsGameSession game = await rpsGameService.CreateGameAsync(Context.User.Id, opponent.Id);

        MessageComponent buttons = new ComponentBuilder().WithButton("Rock", GetCustomId(game.GameId, RpsPick.Rock))
            .WithButton("Paper", GetCustomId(game.GameId, RpsPick.Paper))
            .WithButton("Scissors", GetCustomId(game.GameId, RpsPick.Scissors))
            .Build();

        await FollowupAsync(
            $"{Context.User.Mention} challenged {opponent.Mention} to Rock-Paper-Scissors.\nBoth players choose using the buttons below.",
            components: buttons);
    }

    [ComponentInteraction($"{CustomIdPrefix}:*:*", true)]
    public async Task ChooseAsync(string gameId, string pickRaw)
    {
        ILogger timingLogger = CreateTimingLogger("choose");

        timingLogger.Information(
            "interaction_diag diag_stage={diag_stage} diag_outcome={diag_outcome} total_ms={total_ms} has_responded={has_responded}",
            "choose_start",
            "start",
            0,
            Context.Interaction.HasResponded);

        if (!await TryDeferEphemeralAsync())
            return;

        if (!TryParsePick(pickRaw, out RpsPick pick))
        {
            await SendEphemeralAsync("Invalid pick.");
            return;
        }

        RpsPickSubmissionResult result = await rpsGameService.SubmitPickAsync(gameId, Context.User.Id, pick);

        if (result.Status == RpsPickSubmissionStatus.GameNotFound)
        {
            await SendEphemeralAsync("That game is no longer active.");
            return;
        }

        if (result.Status == RpsPickSubmissionStatus.UnauthorizedUser)
        {
            await SendEphemeralAsync("You are not part of this game.");
            return;
        }

        await SendEphemeralAsync($"Locked in: **{pick}**.");

        if (result.Status == RpsPickSubmissionStatus.PickRecorded)
            return;

        RpsGameSession completedGame =
            result.Game ?? throw new InvalidOperationException("Completed RPS result missing game.");

        RpsGameOutcome outcome =
            result.Outcome ?? throw new InvalidOperationException("Completed RPS result missing outcome.");

        await FollowupAsync(
            $"Game complete: <@{completedGame.ChallengerId}> picked **{completedGame.ChallengerPick}**, <@{completedGame.OpponentId}> picked **{completedGame.OpponentPick}**.\n{GetResultText(outcome)}");
    }

    private ILogger CreateTimingLogger(string operation) =>
        CreateMethodLogger(nameof(CreateTimingLogger))
            .ForContext("diag_event", DiagEventName)
            .ForContext("diag_component", "rps_module")
            .ForContext("rps_operation", operation)
            .ForContext(
                "interaction_age_ms",
                Math.Round(DateTimeOffset.UtcNow.Subtract(Context.Interaction.CreatedAt).TotalMilliseconds, 2));
}