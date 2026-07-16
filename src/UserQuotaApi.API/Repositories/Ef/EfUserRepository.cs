namespace UserQuotaApi.API.Repositories.Ef;

public class EfUserRepository(AppDbContext context) : IUserRepository
{
    // AppDbContext implements IUnitOfWork — all repositories sharing this
    // scoped instance commit together in one SaveChangesAsync() call.
    public IUnitOfWork UnitOfWork => context;

    public async Task<User?> GetByIdAsync(int id) =>
        await context.Users.FindAsync(id);

    public Task<User> CreateAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        context.Users.Add(user);         // stage only — caller calls UnitOfWork.SaveChangesAsync()
        return Task.FromResult(user);
    }

    public async Task<User?> UpdateAsync(int id, User updated)
    {
        var user = await context.Users.FindAsync(id);
        if (user is null) return null;

        user.Name = updated.Name;
        user.Email = updated.Email;      // stage only
        return user;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var user = await context.Users.FindAsync(id);
        if (user is null) return false;

        context.Users.Remove(user);      // stage only
        return true;
    }
}
