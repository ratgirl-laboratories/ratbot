using System.Collections.Concurrent;
using RatBot.Application.Rps;

namespace RatBot.Infrastructure.Stores;

public sealed class RpsGameStore : IRpsGameStore
{
    private readonly ConcurrentDictionary<string, RpsGameSession> _games =
        new ConcurrentDictionary<string, RpsGameSession>(StringComparer.Ordinal);

    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public void Create(RpsGameSession game) => _games[game.GameId] = game;

    public async Task<ErrorOr<RpsPickSubmissionResult>> SubmitPickAsync(
        string gameId,
        ulong userId,
        RpsPick pick,
        DateTimeOffset utcNow)
    {
        await _semaphore.WaitAsync();

        try
        {
            if (!_games.TryGetValue(gameId, out RpsGameSession? game))
                return RpsErrors.GameNotFound;

            if (game.ExpiresAt <= utcNow)
            {
                _games.TryRemove(gameId, out _);
                return RpsErrors.GameNotFound;
            }

            bool isChallenger = userId == game.ChallengerId;
            bool isOpponent = userId == game.OpponentId;

            if (!isChallenger && !isOpponent)
                return RpsErrors.UnauthorizedUser;

            RpsGameSession updatedGame = isChallenger
                ? game with { ChallengerPick = pick }
                : game with { OpponentPick = pick };

            _games[gameId] = updatedGame;

            if (updatedGame.ChallengerPick is null || updatedGame.OpponentPick is null)
                return new RpsPickSubmissionResult(updatedGame, null);

            _games.TryRemove(gameId, out _);

            RpsGameOutcome outcome = RpsGameRules.DetermineOutcome(
                updatedGame.ChallengerPick.Value,
                updatedGame.OpponentPick.Value);

            return new RpsPickSubmissionResult(updatedGame, outcome);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}