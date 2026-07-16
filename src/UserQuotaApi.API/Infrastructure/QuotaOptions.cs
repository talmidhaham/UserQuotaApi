namespace UserQuotaApi.API.Infrastructure;

/// <summary>
/// Strongly typed configuration for quota behaviour.
/// Bound from the "Quota" section of appsettings.json.
/// </summary>
public class QuotaOptions
{
    public const string Section = "Quota";

    /// <summary>Maximum requests allowed per user before a 429 is returned.</summary>
    public int MaxRequests { get; set; } = 5;

    /// <summary>
    /// How many times TryConsumeAsync retries after a DbUpdateConcurrencyException
    /// before giving up and rethrowing. Only relevant for the EF (daytime) path.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
