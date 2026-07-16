namespace UserQuotaApi.IntegrationTests.Fixtures;

/// <summary>
/// Base WebApplicationFactory for integration tests.
/// - Replaces the SQLite file DB with a named in-memory SQLite DB (isolated per factory instance).
/// - Swaps IDataSourceSelector for a fixed fake so tests target a specific strategy branch.
/// - Opens a persistent SqliteConnection to prevent the in-memory DB from being dropped
///   between HTTP requests (in-memory DBs are destroyed when the last connection closes).
/// </summary>
public abstract class ApiFactory : WebApplicationFactory<Program>
{
    // Each factory instance gets its own isolated in-memory database name.
    private readonly string _dbName = $"testdb_{Guid.NewGuid():N}";
    private SqliteConnection? _keepAliveConnection;

    protected abstract bool IsDaytime { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Open the persistent connection before the app builds so the DB survives
        // across the EnsureCreatedAsync call and all subsequent HTTP requests.
        _keepAliveConnection = new SqliteConnection(ConnectionString);
        _keepAliveConnection.Open();

        // Override the connection string so DatabaseExtensions.AddDatabase picks it up.
        builder.ConfigureAppConfiguration(cfg =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:QuotaDb"] = ConnectionString
            }));

        builder.ConfigureTestServices(services =>
        {
            // Pin the selector to the strategy branch under test.
            services.RemoveAll<IDataSourceSelector>();
            services.AddSingleton<IDataSourceSelector>(new FixedDataSourceSelector(IsDaytime));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _keepAliveConnection?.Dispose();
        base.Dispose(disposing);
    }

    private string ConnectionString => $"Data Source={_dbName};Mode=Memory;Cache=Shared";
}

/// <summary>Test double that returns a fixed IsDaytime value.</summary>
internal sealed class FixedDataSourceSelector(bool isDaytime) : IDataSourceSelector
{
    public bool IsDaytime() => isDaytime;
}
