namespace UserQuotaApi.API.Infrastructure;

/// <summary>
/// No-op Unit of Work for the in-memory (Elasticsearch mock) repositories.
/// In-memory operations are immediate — there is no transaction to commit.
/// </summary>
public sealed class NoOpUnitOfWork : IUnitOfWork
{
    public static readonly NoOpUnitOfWork Instance = new();

    private NoOpUnitOfWork() { }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}
