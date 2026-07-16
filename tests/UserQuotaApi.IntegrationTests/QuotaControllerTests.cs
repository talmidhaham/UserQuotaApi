using UserQuotaApi.IntegrationTests.Fixtures;
using Xunit.Abstractions;

namespace UserQuotaApi.IntegrationTests;

/// <summary>
/// Quota tests routed through EfQuotaRepository (daytime / SQLite in-memory).
/// Verifies the EF optimistic-concurrency path enforces the limit correctly.
/// </summary>
public class QuotaControllerDaytimeTests(DaytimeFactory factory, ITestOutputHelper output) : IClassFixture<DaytimeFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetAll_Returns200WithJsonArray()
    {
        output.WriteLine("[GET] GET /api/quota...");
        var response = await _client.GetAsync("/api/quota");
        output.WriteLine($"[GET] Response: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Consume_FirstRequest_Returns200()
    {
        var userId = await CreateUserAsync();

        output.WriteLine($"[POST] Consuming quota for user {userId} (attempt 1/5)...");
        var response = await _client.PostAsync($"/api/quota/consume/{userId}", null);
        output.WriteLine($"[POST] Response: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Consume_WithinLimit_AllSucceed()
    {
        var userId = await CreateUserAsync();

        output.WriteLine($"[POST] Consuming all 5 quota units for user {userId}...");
        for (var i = 0; i < 5; i++)
        {
            output.WriteLine($"[POST] Attempt {i + 1}/5...");
            var r = await _client.PostAsync($"/api/quota/consume/{userId}", null);
            output.WriteLine($"[POST] Response: {(int)r.StatusCode} {r.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }
    }

    [Fact]
    public async Task Consume_ExactlyAtLimit_NextReturns429()
    {
        var userId = await CreateUserAsync();

        output.WriteLine($"[POST] Exhausting quota for user {userId} (5 requests)...");
        for (var i = 0; i < 5; i++)
        {
            output.WriteLine($"[POST] Attempt {i + 1}/5...");
            await _client.PostAsync($"/api/quota/consume/{userId}", null);
        }

        output.WriteLine($"[POST] Attempt 6 (over limit) — expecting 429...");
        var response = await _client.PostAsync($"/api/quota/consume/{userId}", null);
        output.WriteLine($"[POST] Response: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    // --- helpers ---

    private async Task<int> CreateUserAsync()
    {
        output.WriteLine("[POST] Creating user for quota test...");
        var response = await _client.PostAsJsonAsync("/api/users",
            new CreateUserRequest($"User_{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@test.com"));
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<UserDto>())!.Id;
        output.WriteLine($"[POST] User created → Id={id}, quota initialized");
        return id;
    }

    private record UserDto(int Id);
}

/// <summary>
/// Quota tests routed through InMemoryQuotaRepository (nighttime / in-process mock).
/// Covers the same HTTP contract plus a concurrency test unique to this branch.
/// </summary>
public class QuotaControllerNighttimeTests(NighttimeFactory factory, ITestOutputHelper output) : IClassFixture<NighttimeFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Consume_FirstRequest_Returns200()
    {
        var userId = await CreateUserAsync();

        output.WriteLine($"[POST] Consuming quota for user {userId} (nighttime → InMemory)...");
        var response = await _client.PostAsync($"/api/quota/consume/{userId}", null);
        output.WriteLine($"[POST] Response: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Consume_ExactlyAtLimit_NextReturns429()
    {
        var userId = await CreateUserAsync();

        output.WriteLine($"[POST] Exhausting quota for user {userId} (5 requests, nighttime)...");
        for (var i = 0; i < 5; i++)
        {
            output.WriteLine($"[POST] Attempt {i + 1}/5...");
            await _client.PostAsync($"/api/quota/consume/{userId}", null);
        }

        output.WriteLine($"[POST] Attempt 6 (over limit) — expecting 429...");
        var response = await _client.PostAsync($"/api/quota/consume/{userId}", null);
        output.WriteLine($"[POST] Response: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Consume_Concurrent_ExactlyMaxRequestsSucceed()
    {
        const int maxRequests = 5;
        const int totalAttempts = 30;

        var userId = await CreateUserAsync();

        output.WriteLine($"[POST] Firing {totalAttempts} concurrent consume requests for user {userId}...");
        var tasks = Enumerable.Range(0, totalAttempts)
            .Select(_ => _client.PostAsync($"/api/quota/consume/{userId}", null));
        var responses = await Task.WhenAll(tasks);

        var ok  = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var too = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        output.WriteLine($"[POST] Results — 200 OK: {ok}, 429 TooManyRequests: {too}");
        output.WriteLine($"[POST] Expected — 200 OK: {maxRequests}, 429 TooManyRequests: {totalAttempts - maxRequests}");

        Assert.Equal(maxRequests,                 ok);
        Assert.Equal(totalAttempts - maxRequests, too);
    }

    // --- helpers ---

    private async Task<int> CreateUserAsync()
    {
        output.WriteLine("[POST] Creating user for nighttime quota test...");
        var response = await _client.PostAsJsonAsync("/api/users",
            new CreateUserRequest($"User_{Guid.NewGuid():N}", $"{Guid.NewGuid():N}@test.com"));
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<UserDto>())!.Id;
        output.WriteLine($"[POST] User created → Id={id}, quota initialized in InMemoryQuotaRepository");
        return id;
    }

    private record UserDto(int Id);
}
