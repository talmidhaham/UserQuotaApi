namespace UserQuotaApi.API.Infrastructure;

/// <summary>
/// Unit of Work contract — callers control WHEN changes are flushed to the database.
/// Both EF Core and the in-memory mock implement this interface so controllers
/// can call SaveChangesAsync() without knowing which store is active.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
