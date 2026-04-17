using Microsoft.EntityFrameworkCore.ChangeTracking;
using RatBot.Application.Features.Meta.Interfaces;
using RatBot.Domain.Primitives;
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
        ChannelSnowflake threadChannelId,
        CancellationToken ct = default)
    {
        MetaSuggestion? suggestion = await dbContext
            .Set<MetaSuggestion>()
            .SingleOrDefaultAsync(x => x.Id == suggestionId, ct);

        if (suggestion is null)
            return Error.NotFound(description: "Meta suggestion not found.");

        ErrorOr<Success> attachResult = suggestion.AttachThread(threadChannelId);

        if (attachResult.IsError)
            return attachResult.Errors;

        await dbContext.SaveChangesAsync(ct);
        return Result.Success;
    }
}
