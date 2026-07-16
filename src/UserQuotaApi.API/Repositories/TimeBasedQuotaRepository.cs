namespace UserQuotaApi.API.Repositories;

/// <summary>
/// Strategy router: delegates to EF Core (daytime) or in-memory mock (nighttime).
/// Scoped lifetime — receives the scoped EF repo and the singleton in-memory repo.
/// </summary>
public class TimeBasedQuotaRepository(
    IDataSourceSelector selector,
    EfQuotaRepository efRepo,
    InMemoryQuotaRepository inMemoryRepo) : IQuotaRepository
{
    private IQuotaRepository Active => selector.IsDaytime() ? efRepo : inMemoryRepo;

    public IUnitOfWork UnitOfWork => Active.UnitOfWork;

    public Task<IEnumerable<QuotaRecord>> GetAllAsync() => Active.GetAllAsync();
    public Task<QuotaRecord?> GetByUserIdAsync(int userId) => Active.GetByUserIdAsync(userId);
    public Task<bool> TryConsumeAsync(int userId) => Active.TryConsumeAsync(userId);
    public Task InitializeForUserAsync(int userId) => Active.InitializeForUserAsync(userId);
}
