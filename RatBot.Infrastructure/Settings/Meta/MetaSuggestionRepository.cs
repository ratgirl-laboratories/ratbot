using Microsoft.EntityFrameworkCore.ChangeTracking;
using RatBot.Application.Features.Meta.Interfaces;
using RatBot.Infrastructure.Data;

namespace RatBot.Infrastructure.Settings.Meta;

public sealed class MetaSuggestionRepository(BotDbContext dbContext) : IMetaSuggestionRepository
{
    public async Task<ErrorOr<MetaSuggestion>> CreateAsync(MetaSuggestion suggestion, CancellationToken ct = default)
    {
        EntityEntry<MetaSuggestion> entry = await dbContext.Set<MetaSuggestion>().AddAsync(suggestion, ct);
        await dbContext.SaveChangesAsync(ct);

        return entry.Entity.Id > 0
            ? entry.Entity
            : Error.Failure(
                "MetaSuggestion.PersistenceFailed",
                "Suggestion row was saved without a valid database identifier.");
    }

    public async Task<ErrorOr<Success>> AttachThreadLinkageAsync(
        long suggestionId,
        ulong threadChannelId,
        CancellationToken ct = default)
    {
        int updatedRows = await dbContext
            .Set<MetaSuggestion>()
            .Where(x => x.Id == suggestionId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.ThreadChannelId, threadChannelId),
                ct);

        if (updatedRows == 0)
            return Error.NotFound(description: "Meta suggestion not found.");

        return Result.Success;
    }
}
