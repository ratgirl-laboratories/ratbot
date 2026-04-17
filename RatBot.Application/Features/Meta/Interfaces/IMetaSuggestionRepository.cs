using RatBot.Domain.Primitives;

namespace RatBot.Application.Features.Meta.Interfaces;

public interface IMetaSuggestionRepository
{
    Task<ErrorOr<MetaSuggestion>> CreateAsync(MetaSuggestion suggestion, CancellationToken ct = default);

    Task<ErrorOr<Success>> AttachThreadLinkageAsync(
        long suggestionId,
        ChannelSnowflake threadChannelId,
        CancellationToken ct = default);
}
