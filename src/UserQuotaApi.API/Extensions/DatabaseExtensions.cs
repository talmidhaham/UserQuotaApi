using UserQuotaApi.API.Infrastructure;
using UserQuotaApi.API.Extensions;

namespace UserQuotaApi.API.Extensions;

public static class DatabaseExtensions
{
    /// <summary>
    /// Registers AppDbContext with SQLite.
    /// Connection string key: "QuotaDb" (defaults to "Data Source=quota.db").
    ///
    /// Note on LocalDB: replace UseSqlite with UseSqlServer and set a LocalDB connection string,
    /// e.g. "Server=(localdb)\\mssqllocaldb;Database=QuotaDb;Trusted_Connection=True"
    /// No Aspire resource is needed for either SQLite or LocalDB — they run in-process.
    /// </summary>
    public static IHostApplicationBuilder AddDatabase(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = builder.Configuration.GetConnectionString("QuotaDb")
                ?? "Data Source=quota.db";
            options.UseSqlite(connectionString);
        });
        return builder;
    }

    /// <summary>Creates the schema on first run (no migrations needed for the interview).</summary>
    public static async Task MigrateDbAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.EnsureCreatedAsync();
    }
}
