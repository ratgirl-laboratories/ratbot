using RatBot.Application.Common.Discord;
using RatBot.Application.Features.Meta.Errors;
using RatBot.Application.Features.Meta.Interfaces;
using RatBot.Application.Features.Meta.Models;

namespace RatBot.Application.Features.Meta.Services;

public sealed class MetaSuggestionService(
    IMetaSuggestionRepository suggestionRepository,
    IMetaSuggestionSettingsRepository settingsRepository,
    ILogger logger)
{
    private readonly ILogger _logger = logger.ForContext<MetaSuggestionService>();

    private static string FormatThreadTitle(long suggestionId, string title) =>
        $"#{suggestionId:D3} - {title}";

    private static string BuildFirstPost(MetaSuggestion suggestion) =>
        $"""
         ## Author
         <@{suggestion.AuthorUserId}>
         ## Date
         <t:{suggestion.SubmittedAtUtc.ToUnixTimeSeconds()}:F>
         ## Anonymity
         {ToAnonymityString(suggestion.Anonymity)}
         ## Summary
         {suggestion.Summary}
         """;

    private static string BuildSecondPost(MetaSuggestion suggestion) =>
        $"""
         ## Motivation
         {suggestion.Motivation}
         """;

    private static string BuildThirdPost(MetaSuggestion suggestion) =>
        $"""
          ## Specification
          {suggestion.Specification}
          """;

    private static string ToAnonymityString(MetaSuggestionAnonymity anonymity) =>
        anonymity switch
        {
            MetaSuggestionAnonymity.Anonymous => "anonymous",
            MetaSuggestionAnonymity.Public => "public",
            _ => throw new ArgumentOutOfRangeException(nameof(anonymity), anonymity, null)
        };

    public async Task<ErrorOr<Success>> SubmitAsync(
        IMetaSuggestionForumService forumService,
        MetaSuggestionDraft draft,
        MetaSuggestionAnonymity anonymity,
        CancellationToken ct = default)
    {
        _logger.Information(
            "Received meta suggestion submission for guild {GuildId} from author {AuthorUserId}.",
            draft.GuildId,
            draft.AuthorUserId);

        ErrorOr<MetaSuggestionSettings> settingsResult =
            await settingsRepository.GetSettingsAsync(draft.GuildId, ct);

        if (settingsResult.IsError)
            return settingsResult.Errors;

        MetaSuggestionSettings settings = settingsResult.Value;

        ErrorOr<MetaSuggestion> suggestionResult = MetaSuggestion.CreateNew(
            draft.GuildId,
            draft.AuthorUserId,
            settings.SuggestForumChannelId,
            draft.Title,
            draft.Summary,
            draft.Motivation,
            draft.Specification,
            anonymity,
            DateTimeOffset.UtcNow);

        if (suggestionResult.IsError)
            return suggestionResult.Errors;

        MetaSuggestion suggestion = suggestionResult.Value;

        ErrorOr<MetaSuggestion> persistedResult = await suggestionRepository.CreateAsync(suggestion, ct);

        if (persistedResult.IsError)
            return persistedResult.Errors;

        MetaSuggestion persisted = persistedResult.Value;

        _logger.Information(
            "Persisted meta suggestion {SuggestionId} for guild {GuildId}.",
            persisted.Id,
            persisted.GuildId);

        string threadTitle = FormatThreadTitle(persisted.Id, persisted.Title);
        string firstPost = BuildFirstPost(persisted);
        string secondPost = BuildSecondPost(persisted);
        string thirdPost = BuildThirdPost(persisted);

        ErrorOr<CreatedMetaSuggestionThread> threadResult = await forumService.CreateSuggestionThreadAsync(
            persisted.ForumChannelId,
            threadTitle,
            firstPost,
            secondPost,
            thirdPost);

        if (threadResult.IsError)
        {
            _logger.Error(
                "Forum thread creation failed for suggestion {SuggestionId} in forum {ForumChannelId}. Error={ErrorCode}",
                persisted.Id,
                persisted.ForumChannelId,
                threadResult.FirstError.Code);

            return MetaSuggestionErrors.ForumThreadCreationFailed(persisted.Id);
        }

        CreatedMetaSuggestionThread createdThread = threadResult.Value;

        _logger.Information(
            "Created forum thread {ThreadChannelId} for suggestion {SuggestionId}.",
            createdThread.ThreadChannelId,
            persisted.Id);

        ErrorOr<Success> linkageResult = await suggestionRepository.AttachThreadLinkageAsync(
            persisted.Id,
            createdThread.ThreadChannelId,
            ct);

        if (linkageResult.IsError)
        {
            _logger.Error(
                "Failed persisting suggestion-thread linkage for suggestion {SuggestionId}, thread {ThreadChannelId}.",
                persisted.Id,
                createdThread.ThreadChannelId);

            return MetaSuggestionErrors.LinkagePersistFailed(persisted.Id);
        }

        _logger.Information(
            "Persisted suggestion-thread linkage for suggestion {SuggestionId}.",
            persisted.Id);

        return Result.Success;
    }
}
