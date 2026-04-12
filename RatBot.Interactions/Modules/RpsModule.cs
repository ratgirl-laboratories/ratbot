using ErrorOr;
using RatBot.Application.Features.Rps;

namespace RatBot.Interactions.Modules;

[UsedImplicitly]
public sealed class RpsModule(RpsGameService rpsGameService) : SlashCommandBase
{
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
        await DeferAsync();

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

        ErrorOr<RpsGameSession> gameResult = rpsGameService.CreateGameAsync(Context.User.Id, opponent.Id);

        if (gameResult.IsError)
        {
            await RespondAsync(gameResult.FirstError.Description, ephemeral: true);
            return;
        }

        RpsGameSession game = gameResult.Value;

        MessageComponent buttons = new ComponentBuilder()
            .WithButton("Rock", GetCustomId(game.GameId, RpsPick.Rock))
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
        await DeferAsync();

        if (!TryParsePick(pickRaw, out RpsPick pick))
        {
            await RespondAsync(
                "Invalid pick. Somehow. Like, you should really not be seeing this error.",
                ephemeral: true);

            return;
        }

        ErrorOr<RpsPickSubmissionResult> result = await rpsGameService.SubmitPickAsync(gameId, Context.User.Id, pick);

        await result.SwitchFirstAsync(
            async success =>
            {
                if (success.Outcome is null)
                {
                    await RespondAsync($"Locked in: **{pick}**.", ephemeral: true);
                    return;
                }

                await RespondAsync("Game complete.", ephemeral: true);

                ulong challengerId = success.Game.ChallengerId;
                RpsPick? challengerPick = success.Game.ChallengerPick;

                ulong opponentId = success.Game.OpponentId;
                RpsPick? opponentPick = success.Game.OpponentPick;

                string resultText = GetResultText(success.Outcome.Value);

                await FollowupAsync(
                    $"Game complete: <@{challengerId}> picked **{challengerPick}**, <@{opponentId}> picked **{opponentPick}**.\n{resultText}");
            },
            async error => await RespondAsync(error.Description, ephemeral: true));
    }
}