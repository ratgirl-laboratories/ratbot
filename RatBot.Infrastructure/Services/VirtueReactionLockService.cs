using RatBot.Domain.Entities;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Services;

public sealed class VirtueReactionLockService
{
    private readonly BotDbContext _dbContext;

    public VirtueReactionLockService(BotDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryLockAsync(
        ulong messageId,
        ulong reactorUserId,
        ulong targetUserId,
        string emojiId,
        int virtueDelta
    )
    {
        VirtueReactionLock? existing = await _dbContext.VirtueReactionLocks.FindAsync(messageId, reactorUserId);
        if (existing is not null)
            return false;

        _dbContext.VirtueReactionLocks.Add(
            new VirtueReactionLock
            {
                MessageId = messageId,
                ReactorUserId = reactorUserId,
                TargetUserId = targetUserId,
                EmojiId = emojiId,
                VirtueDelta = virtueDelta,
                CreatedAtUtc = DateTime.UtcNow,
            }
        );

        try
        {
            await _dbContext.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            // Race-safe behavior: a concurrent request inserted this lock first.
            return false;
        }
    }

    private static bool IsDuplicateKey(DbUpdateException ex)
    {
        string message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase);
    }
}
