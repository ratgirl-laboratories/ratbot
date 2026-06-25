namespace RatBot.Application.Common;

/// <summary>
///     Simple repository pattern interface. Thanks, Zoran!
/// </summary>
/// <typeparam name="TAggregate">The persisted domain aggregate type.</typeparam>
public interface IRepository<TAggregate>
{
    /// <summary>
    ///     Attempts to find an entity of type <typeparamref name="TAggregate" /> by the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to locate.</param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains
    ///     an <c>ErrorOr</c> object that either holds the found entity or an error indicating why the entity could not be
    ///     found.
    /// </returns>
    Task<ErrorOr<TAggregate>> TryFindAsync(long id);

    /// <summary>
    ///     Adds an aggregate entity of type <typeparamref name="TAggregate" /> to the repository.
    /// </summary>
    /// <param name="aggregate">The aggregate entity instance to add.</param>
    void Add(TAggregate aggregate);

    /// <summary>
    ///     Deletes the specified aggregate entity from the repository.
    /// </summary>
    /// <param name="aggregate">The aggregate entity to delete.</param>
    void Delete(TAggregate aggregate);

    /// <summary>
    ///     Deletes an entity of type <typeparamref name="TAggregate" /> from the repository
    ///     using the specified identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    async Task Delete(long id)
    {
        ErrorOr<TAggregate> aggregate = await TryFindAsync(id);
        aggregate.Switch(Delete, null!);
    }
}
