namespace UserQuotaApi.IntegrationTests.Fixtures;

/// <summary>Factory wired to nighttime — quota routes through InMemoryQuotaRepository.</summary>
public sealed class NighttimeFactory : ApiFactory
{
    protected override bool IsDaytime => false;
}
