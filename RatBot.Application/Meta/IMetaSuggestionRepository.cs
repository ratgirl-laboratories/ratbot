namespace RatBot.Application.Meta;

public interface IMetaSuggestionRepository
{
    Task<ErrorOr<MetaSuggestion>> CreateAsync(MetaSuggestion suggestion, CancellationToken ct = default);

    Task<ErrorOr<Success>> AttachThreadLinkageAsync(
        long suggestionId,
        ulong threadChannelId,
        CancellationToken ct = default);
}