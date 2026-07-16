namespace UserQuotaApi.API.Services;

public interface IDataSourceSelector
{
    /// <summary>Returns true when the active data source should be EF Core (MySQL/SQLite).</summary>
    bool IsDaytime();
}
