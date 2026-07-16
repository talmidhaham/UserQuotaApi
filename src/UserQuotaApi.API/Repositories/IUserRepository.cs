namespace UserQuotaApi.API.Repositories;

public interface IUserRepository
{
    IUnitOfWork UnitOfWork { get; }
    Task<User?> GetByIdAsync(int id);
    Task<User> CreateAsync(User user);
    Task<User?> UpdateAsync(int id, User updated);
    Task<bool> DeleteAsync(int id);
}
