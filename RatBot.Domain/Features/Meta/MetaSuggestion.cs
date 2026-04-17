using RatBot.Domain.Primitives;

namespace RatBot.Domain.Features.Meta;

using ErrorOr;

public sealed class MetaSuggestion
{
    // For EF
    private MetaSuggestion()
    {
    }

    public long Id { get; private init; }
    public GuildSnowflake GuildId { get; private init; }
    public UserSnowflake AuthorUserId { get; private init; }
    public DateTimeOffset SubmittedAtUtc { get; private init; }
    public string Title { get; private init; } = string.Empty;
    public string Summary { get; private init; } = string.Empty;
    public string Motivation { get; private init; } = string.Empty;
    public string Specification { get; private init; } = string.Empty;
    public MetaSuggestionAnonymity Anonymity { get; private init; }
    public MetaSuggestionState State { get; private init; }
    public ChannelSnowflake ForumChannelId { get; private init; }
    public ChannelSnowflake? ThreadChannelId { get; private set; }

    public static ErrorOr<MetaSuggestion> CreateNew(
        GuildSnowflake guildId,
        UserSnowflake authorUserId,
        ChannelSnowflake forumChannelId,
        string title,
        string summary,
        string motivation,
        string specification,
        MetaSuggestionAnonymity anonymity,
        DateTimeOffset submittedAtUtc)
    {
        title = title.Trim();
        summary = summary.Trim();
        motivation = motivation.Trim();
        specification = specification.Trim();

        if (string.IsNullOrWhiteSpace(title))
            return RequiredFieldMissing(nameof(Title));

        if (string.IsNullOrWhiteSpace(summary))
            return RequiredFieldMissing(nameof(Summary));

        if (string.IsNullOrWhiteSpace(motivation))
            return RequiredFieldMissing(nameof(Motivation));

        if (string.IsNullOrWhiteSpace(specification))
            return RequiredFieldMissing(nameof(Specification));

        if (!Enum.IsDefined(anonymity))
            return Error.Validation(
                "MetaSuggestion.InvalidAnonymity",
                "Meta suggestion anonymity is invalid.");

        return new MetaSuggestion
        {
            GuildId = guildId,
            AuthorUserId = authorUserId,
            SubmittedAtUtc = submittedAtUtc,
            Title = title,
            Summary = summary,
            Motivation = motivation,
            Specification = specification,
            Anonymity = anonymity,
            State = MetaSuggestionState.New,
            ForumChannelId = forumChannelId,
            ThreadChannelId = null
        };
    }

    public ErrorOr<Success> AttachThread(ChannelSnowflake threadChannelId)
    {
        if (ThreadChannelId is not null)
            return Error.Conflict(
                "MetaSuggestion.ThreadAlreadyAttached",
                "Meta suggestion already has a thread attached.");

        ThreadChannelId = threadChannelId;
        return Result.Success;
    }

    private static Error RequiredFieldMissing(string fieldName) =>
        Error.Validation(
            $"MetaSuggestion.{fieldName}Required",
            $"Meta suggestion {fieldName.ToLowerInvariant()} is required.");
}