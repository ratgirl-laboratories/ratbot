namespace RatBot.Application.Meta;

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

        ErrorOr<Success> result = await repository.SaveSettingsAsync(
            new MetaSuggestionSettings(guildId, forumChannelId),
            ct);

        if (result.IsError)
            return result.Errors;

        _logger.Information(
            "Meta suggestion forum settings updated for guild {GuildId}. ForumChannelId={ForumChannelId}",
            guildId,
            forumChannelId);

        return Result.Success;
    }
}