namespace UserQuotaApi.API.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(
    IUserRepository userRepo,
    IQuotaRepository quotaRepo,
    ILogger<UsersController> logger) : ControllerBase
{
    /// <summary>Creates a user and initializes their quota record.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var user = new User { Name = request.Name, Email = request.Email };
        var created = await userRepo.CreateAsync(user);

        // Flush the user INSERT first so the DB assigns an Id
        await userRepo.UnitOfWork.SaveChangesAsync();

        // Now use the real Id to initialise the quota record, then flush both in one call
        await quotaRepo.InitializeForUserAsync(created.Id);
        await quotaRepo.UnitOfWork.SaveChangesAsync();

        logger.LogInformation("User {UserId} ({Email}) created", created.Id, created.Email);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await userRepo.GetByIdAsync(id);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateUserRequest request)
    {
        var updated = await userRepo.UpdateAsync(id, new User { Name = request.Name, Email = request.Email });
        if (updated is null) return NotFound();

        await userRepo.UnitOfWork.SaveChangesAsync();
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await userRepo.DeleteAsync(id);
        if (!deleted) return NotFound();

        await userRepo.UnitOfWork.SaveChangesAsync();
        return NoContent();
    }
}

public record CreateUserRequest(string Name, string Email);
