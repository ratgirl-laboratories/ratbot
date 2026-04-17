using RatBot.Domain.Emoji;

namespace RatBot.Application.Emoji;

public interface IEmojiRepository
{
    Task RecordBatchUsageAsync(IEnumerable<(string Id, int N)> usages, CancellationToken ct = default);

    Task<ErrorOr<List<EmojiUsageCount>>> GetTopUsageAsync(int limit, CancellationToken ct = default);
}