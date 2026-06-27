using Microsoft.AspNetCore.Mvc;

namespace NetIAM.AuthServer.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            service = "NetIAM.AuthServer",
            status = "healthy",
            time = DateTimeOffset.UtcNow
        });
    }
}
