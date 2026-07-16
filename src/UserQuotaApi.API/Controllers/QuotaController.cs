namespace UserQuotaApi.API.Controllers;

[ApiController]
[Route("api/quota")]
public class QuotaController(
    IQuotaRepository quotaRepo,
    ILogger<QuotaController> logger) : ControllerBase
{
    /// <summary>Returns all quota records.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var quotas = await quotaRepo.GetAllAsync();
        return Ok(quotas);
    }

    /// <summary>
    /// Consumes one quota unit for the user identified by {id}.
    /// Returns 200 on success, 429 when the quota is exhausted.
    /// </summary>
    [HttpPost("consume/{id:int}")]
    public async Task<IActionResult> Consume(int id)
    {
        var consumed = await quotaRepo.TryConsumeAsync(id);

        if (!consumed)
        {
            logger.LogWarning("User {UserId} has exceeded their quota", id);
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                message = "Too Many Requests — quota exhausted for this user."
            });
        }

        logger.LogInformation("Quota consumed for user {UserId}", id);
        return Ok(new { message = "Quota consumed successfully." });
    }
}
