using Microsoft.AspNetCore.Mvc;

namespace NetIAM.PortalApi.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            service = "NetIAM.PortalApi",
            status = "healthy",
            time = DateTimeOffset.UtcNow
        });
    }
}
