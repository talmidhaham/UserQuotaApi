using UserQuotaApi.IntegrationTests.Fixtures;
using Xunit.Abstractions;

namespace UserQuotaApi.IntegrationTests;

/// <summary>
/// Full-stack HTTP tests for UsersController.
/// Uses DaytimeFactory — users always go through EF regardless of time,
/// so the daytime factory is the canonical choice here.
/// Each test creates its own user to avoid shared-state conflicts.
/// </summary>
public class UsersControllerTests(DaytimeFactory factory, ITestOutputHelper output) : IClassFixture<DaytimeFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_ValidRequest_Returns201()
    {
        output.WriteLine("[POST] Creating user Alice...");
        var response = await _client.PostAsJsonAsync("/api/users",
            new CreateUserRequest("Alice", "alice@example.com"));
        output.WriteLine($"[POST] Response: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_ResponseContainsAssignedId()
    {
        output.WriteLine("[POST] Creating user Bob...");
        var response = await _client.PostAsJsonAsync("/api/users",
            new CreateUserRequest("Bob", "bob@example.com"));

        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        output.WriteLine($"[POST] Assigned Id: {user!.Id}");
        Assert.True(user.Id > 0);
    }

    [Fact]
    public async Task Post_LocationHeaderPointsToCreatedUser()
    {
        output.WriteLine("[POST] Creating user Carol...");
        var response = await _client.PostAsJsonAsync("/api/users",
            new CreateUserRequest("Carol", "carol@example.com"));
        output.WriteLine($"[POST] Location: {response.Headers.Location}");

        Assert.NotNull(response.Headers.Location);
        output.WriteLine($"[GET]  Fetching {response.Headers.Location}...");
        var getResponse = await _client.GetAsync(response.Headers.Location);
        output.WriteLine($"[GET]  Response: {(int)getResponse.StatusCode} {getResponse.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Get_ExistingUser_Returns200WithCorrectData()
    {
        var created = await CreateUserAsync("Dave", "dave@example.com");

        output.WriteLine($"[GET]  Fetching user {created.Id}...");
        var response = await _client.GetAsync($"/api/users/{created.Id}");
        var user = await response.Content.ReadFromJsonAsync<UserDto>();
        output.WriteLine($"[GET]  Returned: Id={user!.Id} Name={user.Name} Email={user.Email}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Dave", user.Name);
        Assert.Equal("dave@example.com", user.Email);
    }

    [Fact]
    public async Task Get_UnknownUser_Returns404()
    {
        output.WriteLine("[GET]  Fetching non-existent user 999999...");
        var response = await _client.GetAsync("/api/users/999999");
        output.WriteLine($"[GET]  Response: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_ExistingUser_Returns200WithUpdatedFields()
    {
        var created = await CreateUserAsync("Eve", "eve@example.com");

        output.WriteLine($"[PUT]  Updating user {created.Id} → name='Eve Updated', email='eve.new@example.com'...");
        var response = await _client.PutAsJsonAsync($"/api/users/{created.Id}",
            new CreateUserRequest("Eve Updated", "eve.new@example.com"));
        var updated = await response.Content.ReadFromJsonAsync<UserDto>();
        output.WriteLine($"[PUT]  Response: {(int)response.StatusCode} — Name={updated!.Name} Email={updated.Email}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Eve Updated", updated.Name);
        Assert.Equal("eve.new@example.com", updated.Email);
    }

    [Fact]
    public async Task Put_UnknownUser_Returns404()
    {
        output.WriteLine("[PUT]  Updating non-existent user 999999...");
        var response = await _client.PutAsJsonAsync("/api/users/999999",
            new CreateUserRequest("Ghost", "ghost@example.com"));
        output.WriteLine($"[PUT]  Response: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingUser_Returns204()
    {
        var created = await CreateUserAsync("Frank", "frank@example.com");

        output.WriteLine($"[DELETE] Deleting user {created.Id}...");
        var response = await _client.DeleteAsync($"/api/users/{created.Id}");
        output.WriteLine($"[DELETE] Response: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownUser_Returns404()
    {
        output.WriteLine("[DELETE] Deleting non-existent user 999999...");
        var response = await _client.DeleteAsync("/api/users/999999");
        output.WriteLine($"[DELETE] Response: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ThenGet_Returns404()
    {
        var created = await CreateUserAsync("Grace", "grace@example.com");

        output.WriteLine($"[DELETE] Deleting user {created.Id}...");
        await _client.DeleteAsync($"/api/users/{created.Id}");
        output.WriteLine($"[GET]    Fetching deleted user {created.Id}...");
        var response = await _client.GetAsync($"/api/users/{created.Id}");
        output.WriteLine($"[GET]    Response: {(int)response.StatusCode} {response.StatusCode}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- helpers ---

    private async Task<UserDto> CreateUserAsync(string name, string email)
    {
        output.WriteLine($"[POST] Creating user name='{name}' email='{email}'...");
        var response = await _client.PostAsJsonAsync("/api/users", new CreateUserRequest(name, email));
        response.EnsureSuccessStatusCode();
        var user = (await response.Content.ReadFromJsonAsync<UserDto>())!;
        output.WriteLine($"[POST] User created → Id={user.Id}");
        return user;
    }

    private record UserDto(int Id, string Name, string Email);
}
