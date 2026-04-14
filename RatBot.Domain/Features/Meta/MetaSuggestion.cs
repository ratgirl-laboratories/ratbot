namespace RatBot.Domain.Features.Meta;

public sealed record MetaSuggestion
{
    public long Id { get; init; }
    public ulong GuildId { get; init; }
    public ulong AuthorUserId { get; init; }
    public DateTimeOffset SubmittedAtUtc { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Motivation { get; init; }
    public required string Specification { get; init; }
    public MetaSuggestionAnonymity Anonymity { get; init; }
    public MetaSuggestionState State { get; init; }
    public ulong ForumChannelId { get; init; }
    public ulong? ThreadChannelId { get; init; }

    public MetaSuggestion WithDatabaseId(long id)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Database identifier must be greater than zero.");

        return this with { Id = id };
    }
}
