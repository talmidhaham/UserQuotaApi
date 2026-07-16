using UserQuotaApi.API.Repositories.Ef;
using UserQuotaApi.API.Repositories.InMemory;
using UserQuotaApi.API.Repositories;
using UserQuotaApi.API.Services;

namespace UserQuotaApi.API.Extensions;

public static class RepositoryExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Bind strongly typed quota options from the "Quota" configuration section.
        services.AddOptions<QuotaOptions>().BindConfiguration(QuotaOptions.Section);

        // Users always use EF Core — they are the system's source of truth.
        // Switching the user store at nighttime would make users created during
        // the day invisible at night (no sync), so a single persistent store is correct.
        services.AddScoped<IUserRepository, EfUserRepository>();

        // Quotas use the dual-source strategy (EF Core day / in-memory ES mock night).
        // The in-memory singleton must outlive requests to retain counters.
        services.AddScoped<EfQuotaRepository>();
        services.AddSingleton<InMemoryQuotaRepository>();

        // Time-based strategy selector
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IDataSourceSelector, UtcTimeDataSourceSelector>();

        // Router registered as the public interface used by controllers
        services.AddScoped<IQuotaRepository, TimeBasedQuotaRepository>();

        return services;
    }
}
