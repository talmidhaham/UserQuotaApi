namespace UserQuotaApi.IntegrationTests.Fixtures;

/// <summary>Factory wired to daytime — quota routes through EfQuotaRepository.</summary>
public sealed class DaytimeFactory : ApiFactory
{
    protected override bool IsDaytime => true;
}
