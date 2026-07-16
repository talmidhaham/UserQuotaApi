namespace UserQuotaApi.API.Repositories;

public interface IQuotaRepository
{
    IUnitOfWork UnitOfWork { get; }
    Task<IEnumerable<QuotaRecord>> GetAllAsync();
    Task<QuotaRecord?> GetByUserIdAsync(int userId);

    /// <summary>
    /// Atomically checks and increments the counter.
    /// Returns false (429) when the user has reached their quota.
    /// </summary>
    Task<bool> TryConsumeAsync(int userId);

    /// <summary>Creates the quota record when a user is registered.</summary>
    Task InitializeForUserAsync(int userId);
}
