namespace RatBot.Application.Features.Meta.Errors;

public static class MetaSuggestionErrors
{
    public static readonly Error ForumNotConfigured = Error.NotFound(
        "MetaSuggestion.ForumNotConfigured",
        "Meta suggestions forum is not configured. Ask an admin to run `/config meta suggest <channel>`.");

    public static readonly Error ForumNotFound = Error.NotFound(
        "MetaSuggestion.ForumNotFound",
        "I couldn't find the configured meta suggestions forum channel.");

    public static readonly Error InvalidAnonymityPreference = Error.Validation(
        "MetaSuggestion.InvalidAnonymityPreference",
        "Invalid anonymity preference provided.");

    public static Error ForumThreadCreationFailed(long suggestionId) => Error.Failure(
        "MetaSuggestion.ForumThreadCreationFailed",
        $"Saved suggestion #{suggestionId:D3}, but thread creation failed. The suggestion is recoverable.");

    public static Error LinkagePersistFailed(long suggestionId) => Error.Failure(
        "MetaSuggestion.LinkagePersistFailed",
        $"Created a thread for suggestion #{suggestionId:D3}, but failed to persist linkage.");
}
