namespace RatBot.Application.Rps;

public interface IRpsGameStore
{
    void Create(RpsGameSession game);

    Task<ErrorOr<RpsPickSubmissionResult>> SubmitPickAsync(
        string gameId,
        ulong userId,
        RpsPick pick,
        DateTimeOffset utcNow);
}