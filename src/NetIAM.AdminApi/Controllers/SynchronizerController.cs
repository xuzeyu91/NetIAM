using Microsoft.AspNetCore.Mvc;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/v1/synchronizer/event")]
public sealed class SynchronizerController(
    ITenantContextAccessor tenantContextAccessor,
    IDirectorySyncService directorySyncService) : ControllerBase
{
    [HttpPost("{code}")]
    [RequirePermission("source.write")]
    public async Task<IActionResult> Receive(string code, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var tenantId = tenantContextAccessor.GetTenantId();
        var accepted = await directorySyncService.HandleWebhookAsync(code, tenantId, payload, cancellationToken);
        return accepted ? Ok(new { accepted = true }) : NotFound(new { accepted = false });
    }
}
