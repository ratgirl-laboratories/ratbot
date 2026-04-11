using System.Collections.Concurrent;
using ErrorOr;
using RatBot.Application.Features.Rps;

namespace RatBot.Infrastructure.Persistence;

public sealed class InMemoryRpsGameRepository : IRpsGameRepository
{
    private readonly ConcurrentDictionary<string, RpsGameSession> _games =
        new ConcurrentDictionary<string, RpsGameSession>(StringComparer.Ordinal);

    public Task CreateAsync(RpsGameSession game, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        _games[game.GameId] = game;
        return Task.CompletedTask;
    }

    public Task<ErrorOr<RpsPickSubmissionResult>> SubmitPickAsync(
        string gameId,
        ulong userId,
        RpsPick pick,
        DateTimeOffset utcNow,
        CancellationToken ct = default)
    {
        while (true)
        {
            if (!_games.TryGetValue(gameId, out RpsGameSession? game))
                return Task.FromResult<ErrorOr<RpsPickSubmissionResult>>(Error.NotFound(description: "Game not found."));

            if (game.ExpiresAt <= utcNow)
            {
                _games.TryRemove(gameId, out _);
                return Task.FromResult<ErrorOr<RpsPickSubmissionResult>>(Error.NotFound(description: "Game expired."));
            }

            bool isChallenger = userId == game.ChallengerId;
            bool isOpponent = userId == game.OpponentId;

            if (!isChallenger && !isOpponent)
                return Task.FromResult<ErrorOr<RpsPickSubmissionResult>>(Error.Forbidden(description: "User not part of this game."));

            RpsGameSession updatedGame = isChallenger
                ? game with { ChallengerPick = pick }
                : game with { OpponentPick = pick };

            if (!_games.TryUpdate(gameId, updatedGame, game))
                continue;

            if (updatedGame.ChallengerPick is null || updatedGame.OpponentPick is null)
                return Task.FromResult<ErrorOr<RpsPickSubmissionResult>>(new RpsPickSubmissionResult(updatedGame, null));

            _games.TryRemove(gameId, out _);

            RpsGameOutcome outcome = RpsGameRules.DetermineOutcome(
                updatedGame.ChallengerPick.Value,
                updatedGame.OpponentPick.Value);

            return Task.FromResult<ErrorOr<RpsPickSubmissionResult>>(new RpsPickSubmissionResult(updatedGame, outcome));
        }
    }
}