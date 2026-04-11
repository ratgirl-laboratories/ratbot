using ErrorOr;

namespace RatBot.Application.Features.Rps;

public interface IRpsGameRepository
{
    Task CreateAsync(RpsGameSession game, CancellationToken ct = default);

    Task<ErrorOr<RpsPickSubmissionResult>> SubmitPickAsync(
        string gameId,
        ulong userId,
        RpsPick pick,
        DateTimeOffset utcNow,
        CancellationToken ct = default);
}