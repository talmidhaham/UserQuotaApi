namespace UserQuotaApi.API.Repositories.InMemory;

/// <summary>
/// Simulates an Elasticsearch quota index — stored in process memory.
/// Registered as Singleton so quota counters persist across requests.
/// Concurrency: uses lock-per-record to guarantee exact quota enforcement.
/// </summary>
public class InMemoryQuotaRepository : IQuotaRepository
{
    private readonly ConcurrentDictionary<int, QuotaRecord> _store = new();
    private readonly int _maxRequests;
    private int _nextId;

    // In-memory operations are immediate; no transaction concept exists.
    public IUnitOfWork UnitOfWork => NoOpUnitOfWork.Instance;

    public InMemoryQuotaRepository(IOptions<QuotaOptions> options)
    {
        _maxRequests = options.Value.MaxRequests;
    }

    public Task<IEnumerable<QuotaRecord>> GetAllAsync() =>
        Task.FromResult(_store.Values.AsEnumerable());

    public Task<QuotaRecord?> GetByUserIdAsync(int userId) =>
        Task.FromResult(_store.TryGetValue(userId, out var r) ? r : null);

    public Task InitializeForUserAsync(int userId)
    {
        _store.GetOrAdd(userId, id => new QuotaRecord
        {
            Id = Interlocked.Increment(ref _nextId),
            UserId = id,
            ConsumedCount = 0,
            MaxRequests = _maxRequests
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// Lock-per-record ensures no two concurrent requests can both pass the quota check
    /// for the same user, preventing over-consumption under high concurrency.
    /// </summary>
    public Task<bool> TryConsumeAsync(int userId)
    {
        var quota = _store.GetOrAdd(userId, id => new QuotaRecord
        {
            Id = Interlocked.Increment(ref _nextId),
            UserId = id,
            ConsumedCount = 0,
            MaxRequests = _maxRequests
        });

        lock (quota)
        {
            if (quota.ConsumedCount >= quota.MaxRequests)
                return Task.FromResult(false);

            quota.ConsumedCount++;
            quota.LastConsumedAt = DateTime.UtcNow;
            return Task.FromResult(true);
        }
    }
}
