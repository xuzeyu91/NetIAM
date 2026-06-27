using Microsoft.AspNetCore.Mvc;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            service = "NetIAM.AdminApi",
            status = "healthy",
            time = DateTimeOffset.UtcNow
        });
    }
}
