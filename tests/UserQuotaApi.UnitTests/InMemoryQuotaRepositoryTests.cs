using UserQuotaApi.API.Repositories.InMemory;

namespace UserQuotaApi.UnitTests;

public class InMemoryQuotaRepositoryTests
{
    private static InMemoryQuotaRepository Build(int maxRequests = 3)
    {
        var options = Options.Create(new QuotaOptions { MaxRequests = maxRequests });
        return new InMemoryQuotaRepository(options);
    }

    [Fact]
    public async Task TryConsumeAsync_WithinLimit_ReturnsTrue()
    {
        var repo = Build(maxRequests: 3);
        await repo.InitializeForUserAsync(1);

        var result = await repo.TryConsumeAsync(1);

        Assert.True(result);
    }

    [Fact]
    public async Task TryConsumeAsync_ExceedsLimit_ReturnsFalse()
    {
        var repo = Build(maxRequests: 2);
        await repo.InitializeForUserAsync(1);

        await repo.TryConsumeAsync(1);
        await repo.TryConsumeAsync(1);
        var result = await repo.TryConsumeAsync(1); // 3rd — over limit

        Assert.False(result);
    }

    [Fact]
    public async Task TryConsumeAsync_ExactlyAtLimit_ReturnsFalse()
    {
        const int max = 5;
        var repo = Build(maxRequests: max);
        await repo.InitializeForUserAsync(1);

        for (var i = 0; i < max; i++)
            await repo.TryConsumeAsync(1);

        Assert.False(await repo.TryConsumeAsync(1));
    }

    [Fact]
    public async Task TryConsumeAsync_Concurrent_NeverExceedsLimit()
    {
        const int maxRequests = 5;
        const int totalAttempts = 50;
        var repo = Build(maxRequests: maxRequests);
        await repo.InitializeForUserAsync(1);

        // Fire 50 concurrent requests for the same user
        var tasks = Enumerable.Range(0, totalAttempts)
            .Select(_ => repo.TryConsumeAsync(1));
        var results = await Task.WhenAll(tasks);

        Assert.Equal(maxRequests, results.Count(r => r));          // exactly max succeed
        Assert.Equal(totalAttempts - maxRequests, results.Count(r => !r)); // rest blocked
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllInitializedUsers()
    {
        var repo = Build();
        await repo.InitializeForUserAsync(1);
        await repo.InitializeForUserAsync(2);
        await repo.InitializeForUserAsync(3);

        var all = (await repo.GetAllAsync()).ToList();

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task GetByUserIdAsync_ExistingUser_ReturnsRecord()
    {
        var repo = Build();
        await repo.InitializeForUserAsync(42);

        var record = await repo.GetByUserIdAsync(42);

        Assert.NotNull(record);
        Assert.Equal(42, record.UserId);
    }

    [Fact]
    public async Task GetByUserIdAsync_UnknownUser_ReturnsNull()
    {
        var repo = Build();

        var record = await repo.GetByUserIdAsync(999);

        Assert.Null(record);
    }
}
