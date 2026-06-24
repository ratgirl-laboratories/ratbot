namespace RatBot.Application.Common;

public interface IUnitOfWork
{
    IRepository<TAggregate> GetRepository<TAggregate>();
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
