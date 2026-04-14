using RatBot.Application.Features.Meta.Errors;
using RatBot.Application.Features.Meta.Interfaces;

namespace RatBot.Application.Features.Meta.Services;

public sealed class MetaSuggestionSettingsService(IMetaSuggestionSettingsRepository repository, ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<MetaSuggestionSettingsService>();

    public async Task<ErrorOr<Success>> UpsertSuggestForumChannelAsync(
        ulong guildId,
        ulong forumChannelId,
        CancellationToken ct = default)
    {
        if (forumChannelId == 0)
            return MetaSuggestionErrors.ForumNotFound;

        ErrorOr<bool> result = await repository.UpsertSuggestForumChannelAsync(guildId, forumChannelId, ct);

        if (result.IsError)
            return result.Errors;

        bool created = result.Value;

        _logger.Information(
            "Meta suggestion forum settings {Action} for guild {GuildId}. ForumChannelId={ForumChannelId}",
            created
                ? "created"
                : "updated",
            guildId,
            forumChannelId);

        return Result.Success;
    }
}
