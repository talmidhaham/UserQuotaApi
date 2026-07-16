namespace UserQuotaApi.API.Services;

/// <summary>
/// Selects the active data source based on UTC time:
///   09:00–16:59 UTC → EF Core (daytime)
///   otherwise       → In-memory Elasticsearch mock (nighttime)
/// </summary>
public class UtcTimeDataSourceSelector(TimeProvider timeProvider) : IDataSourceSelector
{
    public bool IsDaytime()
    {
        var hour = timeProvider.GetUtcNow().Hour;
        return hour >= 9 && hour < 17;
    }
}
