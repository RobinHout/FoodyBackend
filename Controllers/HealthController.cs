using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodyBackend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(DatabaseContext context) : ControllerBase
{
    [HttpGet]
    public IActionResult GetApiHealth()
    {
        return Ok(new
        {
            status = "ok",
            service = "FoodyBackend"
        });
    }

    [HttpGet("db")]
    public async Task<IActionResult> GetDatabaseHealth()
    {
        try
        {
            var canConnect = await context.Database.CanConnectAsync();
            var pendingMigrations = canConnect
                ? await context.Database.GetPendingMigrationsAsync()
                : Array.Empty<string>();

            return Ok(new
            {
                status = canConnect ? "ok" : "unreachable",
                database = canConnect ? "connected" : "disconnected",
                pendingMigrations = pendingMigrations.Count()
            });
        }
        catch (Exception exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "error",
                database = "disconnected",
                error = exception.Message
            });
        }
    }
}
