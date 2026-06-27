using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/audit")]
public sealed class AuditController(
    NetIamDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor) : ControllerBase
{
    [HttpGet]
    [RequirePermission("audit.read")]
    public async Task<IActionResult> List([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var normalizedTake = Math.Clamp(take, 1, 500);
        var entries = await dbContext.AuditEvents
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.OccurredTime)
            .Take(normalizedTake)
            .ToListAsync(cancellationToken);

        return Ok(entries);
    }
}
