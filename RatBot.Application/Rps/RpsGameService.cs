namespace RatBot.Application.Rps;

public sealed class RpsGameService(IRpsGameStore store, ILogger logger)
{
    private static readonly TimeSpan GameTtl = TimeSpan.FromMinutes(10);

    private readonly ILogger _logger = logger.ForContext<RpsGameService>();

    public ErrorOr<RpsGameSession> CreateGameAsync(ulong challengerId, ulong opponentId)
    {
        if (challengerId == opponentId)
            return RpsErrors.SameUser;

        DateTime createdAt = DateTime.UtcNow;

        RpsGameSession game = new RpsGameSession(
            Guid.CreateVersion7().ToString("N"),
            challengerId,
            opponentId,
            createdAt.Add(GameTtl),
            null,
            null);

        store.Create(game);

        _logger.Information(
            "Created RPS game {GameId} between challenger {ChallengerId} and opponent {OpponentId}.",
            game.GameId,
            game.ChallengerId,
            game.OpponentId
        );

        return game;
    }

    public Task<ErrorOr<RpsPickSubmissionResult>> SubmitPickAsync(string gameId, ulong userId, RpsPick pick) =>
        store.SubmitPickAsync(gameId, userId, pick, DateTime.UtcNow);
}