using Microsoft.AspNetCore.Mvc;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;

namespace Limita.API.Controllers;

public class HealthController : BaseApiController
{
    private readonly IApplicationDbContext _context;

    public HealthController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<object>>> GetHealth()
    {
        bool canConnectToDb;
        try
        {
            canConnectToDb = await _context.Database.CanConnectAsync();
        }
        catch
        {
            canConnectToDb = false;
        }

        var healthData = new
        {
            Status = "Healthy",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            Framework = ".NET 9 (net9.0)",
            DatabaseConnected = canConnectToDb,
            ServerTimeUtc = DateTime.UtcNow
        };

        return Ok(ApiResponse<object>.Ok(healthData, "Limita Banking API is online."));
    }
}
