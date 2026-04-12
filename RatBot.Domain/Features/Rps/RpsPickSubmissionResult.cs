namespace RatBot.Domain.Features.Rps;

public sealed record RpsPickSubmissionResult(
    RpsGameSession Game,
    RpsGameOutcome? Outcome);