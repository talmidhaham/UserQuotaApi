namespace UserQuotaApi.API.Repositories.Ef;

/// <summary>
/// EF Core / SQLite quota repository.
/// Concurrency: uses optimistic locking via the Version concurrency token + retry loop.
/// </summary>
public class EfQuotaRepository(AppDbContext context, IOptions<QuotaOptions> options) : IQuotaRepository
{
    private readonly QuotaOptions _quota = options.Value;

    // AppDbContext implements IUnitOfWork
    public IUnitOfWork UnitOfWork => context;

    public async Task<IEnumerable<QuotaRecord>> GetAllAsync() =>
        await context.Quotas.AsNoTracking().ToListAsync();

    public async Task<QuotaRecord?> GetByUserIdAsync(int userId) =>
        await context.Quotas.AsNoTracking().FirstOrDefaultAsync(q => q.UserId == userId);

    public async Task InitializeForUserAsync(int userId)
    {
        var exists = await context.Quotas.AnyAsync(q => q.UserId == userId);
        if (exists) return;

        context.Quotas.Add(new QuotaRecord
        {
            UserId = userId,
            ConsumedCount = 0,
            MaxRequests = _quota.MaxRequests
        });                               // stage only — caller calls UnitOfWork.SaveChangesAsync()
    }

    /// <summary>
    /// TryConsumeAsync saves internally because the optimistic-concurrency retry loop
    /// must read → check → increment → save atomically. This is the one exception
    /// to the UoW pattern where the operation owns its own transaction boundary.
    /// </summary>
    public async Task<bool> TryConsumeAsync(int userId)
    {
        var maxRetries = _quota.MaxRetries;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            var quota = await context.Quotas
                .FirstOrDefaultAsync(q => q.UserId == userId);

            if (quota is null) return false;
            if (quota.ConsumedCount >= _quota.MaxRequests) return false;

            quota.ConsumedCount++;
            quota.Version++;        // bumping Version triggers the concurrency check
            quota.LastConsumedAt = DateTime.UtcNow;

            try
            {
                await context.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (attempt == maxRetries - 1) throw;

                // Reload the conflicting entity and retry
                foreach (var entry in ex.Entries)
                    await entry.ReloadAsync();
            }
        }

        // Unreachable: the loop either returns true, returns false on the limit check,
        // or throws on the final retry. Satisfies the compiler's return-path analysis.
        throw new InvalidOperationException("TryConsumeAsync retry loop exhausted without a result.");
    }
}
