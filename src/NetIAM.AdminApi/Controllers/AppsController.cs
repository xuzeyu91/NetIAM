using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Entities;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/apps")]
public sealed class AppsController(
    NetIamDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    IAuditService auditService) : ControllerBase
{
    public sealed record CreateAppRequest(string Code, string Name, string Protocol);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var apps = await dbContext.Apps
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
        return Ok(apps);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAppRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = new AppEntity
        {
            TenantId = tenantId,
            Code = request.Code,
            Name = request.Name,
            Protocol = request.Protocol,
            Enabled = true
        };
        dbContext.Apps.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.app.created",
                $"App {request.Code} created."),
            cancellationToken);

        return Ok(entity);
    }
}
