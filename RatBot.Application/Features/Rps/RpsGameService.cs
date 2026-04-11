using ErrorOr;

namespace RatBot.Application.Features.Rps;

public sealed class RpsGameService(IRpsGameRepository repository, ILogger logger)
{
    private static readonly TimeSpan GameTtl = TimeSpan.FromMinutes(10);

    private readonly ILogger _logger = logger.ForContext<RpsGameService>();

    public async Task<RpsGameSession> CreateGameAsync(
        ulong challengerId,
        ulong opponentId,
        CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(challengerId);
        ArgumentOutOfRangeException.ThrowIfZero(opponentId);

        if (challengerId == opponentId)
            throw new ArgumentException("Challenger and opponent must be different users.", nameof(opponentId));

        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        RpsGameSession game = new RpsGameSession(
            Guid.NewGuid().ToString("N"),
            challengerId,
            opponentId,
            createdAt,
            createdAt.Add(GameTtl),
            null,
            null);

        await repository.CreateAsync(game, ct);

        _logger.Information(
            "Created RPS game {GameId} between challenger {ChallengerId} and opponent {OpponentId}.",
            game.GameId,
            game.ChallengerId,
            game.OpponentId);

        return game;
    }

    public Task<ErrorOr<RpsPickSubmissionResult>> SubmitPickAsync(
        string gameId,
        ulong userId,
        RpsPick pick,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentOutOfRangeException.ThrowIfZero(userId);

        return repository.SubmitPickAsync(gameId, userId, pick, DateTimeOffset.UtcNow, ct);
    }
}