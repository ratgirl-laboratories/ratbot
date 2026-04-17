namespace RatBot.Domain.Rps;

public static class RpsGameRules
{
    public static RpsGameOutcome DetermineOutcome(RpsPick challengerPick, RpsPick opponentPick) =>
        (challengerPick, opponentPick) switch
        {
            (RpsPick.Paper, RpsPick.Rock) => RpsGameOutcome.OpponentWon,
            (RpsPick.Rock, RpsPick.Scissors) => RpsGameOutcome.OpponentWon,
            (RpsPick.Scissors, RpsPick.Paper) => RpsGameOutcome.OpponentWon,
            _ => RpsGameOutcome.Tie
        };
}