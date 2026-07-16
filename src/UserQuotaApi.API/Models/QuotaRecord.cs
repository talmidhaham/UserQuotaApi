namespace UserQuotaApi.API.Models;

public class QuotaRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ConsumedCount { get; set; }
    public int MaxRequests { get; set; }
    public DateTime LastConsumedAt { get; set; }

    /// <summary>
    /// Incremented on every update — used as optimistic concurrency token with EF Core + SQLite.
    /// </summary>
    public int Version { get; set; }
}
