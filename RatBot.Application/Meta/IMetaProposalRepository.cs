namespace RatBot.Application.Meta;

public interface IMetaProposalRepository
{
    /// <summary>
    ///     Finds a meta proposal by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the meta proposal.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>The meta proposal state, or null if not found.</returns>
    Task<MetaProposalState?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    ///     Finds a meta proposal by its suggestion thread channel identifier.
    /// </summary>
    /// <param name="suggestionThreadChannelId">The unique identifier of the suggestion thread channel.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>The meta proposal state, or null if not found.</returns>
    Task<MetaProposalState?> FindBySuggestionThreadAsync(ulong suggestionThreadChannelId, CancellationToken ct = default);

    /// <summary>
    ///     Finds a meta proposal associated with a specified thread channel.
    /// </summary>
    /// <param name="threadChannelId">The unique identifier of the thread channel to search for.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>The meta proposal state, or null if no matching proposal is found.</returns>
    Task<MetaProposalState?> FindByProposalThreadAsync(ulong threadChannelId, CancellationToken ct = default);

    Task<MetaProposalState?> FindByPollMessageAsync(ulong pollMessageId, CancellationToken ct = default);

    /// <summary>
    ///     Retrieves a list of meta proposals that have active polls
    ///     which have expired by the specified date and time.
    /// </summary>
    /// <param name="nowUtc">The current UTC date and time to compare poll expiration times against.</param>
    /// <param name="limit">The maximum number of expired polls to retrieve.</param>
    /// <param name="ct">The cancellation token for the operation.</param>
    /// <returns>A read-only list of meta proposal states with expired polls.</returns>
    Task<IReadOnlyList<MetaProposalState>> FindExpiredPollsAsync(DateTimeOffset nowUtc, int limit, CancellationToken ct = default);

    /// <summary>
    ///     Adds a new meta proposal state to the repository.
    /// </summary>
    /// <param name="state">The meta proposal state to be added.</param>
    void Add(MetaProposalState state);

    /// <summary>
    ///     Removes a specified meta proposal state from the repository.
    /// </summary>
    /// <param name="state">The meta proposal state to be deleted.</param>
    void Delete(MetaProposalState state);
}
