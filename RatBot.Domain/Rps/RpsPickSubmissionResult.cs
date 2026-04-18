namespace RatBot.Domain.Rps;

public sealed record RpsPickSubmissionResult(RpsGameSession Game, RpsGameOutcome? Outcome);