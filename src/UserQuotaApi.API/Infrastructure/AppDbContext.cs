namespace UserQuotaApi.API.Infrastructure;

/// <summary>
/// AppDbContext acts as both the EF Core DbContext and the Unit of Work.
/// DbContext.SaveChangesAsync() satisfies IUnitOfWork — all staged changes
/// across every repository sharing this context are committed in one call.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<QuotaRecord> Quotas => Set<QuotaRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Name).IsRequired().HasMaxLength(100);
            e.Property(u => u.Email).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<QuotaRecord>(e =>
        {
            e.HasKey(q => q.Id);
            e.HasIndex(q => q.UserId).IsUnique();
            e.Property(q => q.UserId).IsRequired();
            // Version acts as optimistic concurrency token (supported by SQLite via manual increment)
            e.Property(q => q.Version).IsConcurrencyToken();
        });
    }
}
