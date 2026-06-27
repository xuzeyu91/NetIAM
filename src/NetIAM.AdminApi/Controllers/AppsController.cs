using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Entities;
using NetIAM.Infrastructure.Authorization;
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
    public sealed record UpdateAppRequest(string Name, string Protocol, bool Enabled);

    [HttpGet]
    [RequirePermission("app.read")]
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
    [RequirePermission("app.write")]
    public async Task<IActionResult> Create([FromBody] CreateAppRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Protocol))
        {
            return BadRequest("Code, Name and Protocol are required.");
        }

        var exists = await dbContext.Apps.AnyAsync(
            x => x.TenantId == tenantId && x.Code == request.Code && !x.IsDeleted,
            cancellationToken);
        if (exists)
        {
            return Conflict($"App code already exists: {request.Code}.");
        }

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

    [HttpPut("{id}")]
    [RequirePermission("app.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateAppRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = await dbContext.Apps
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Protocol))
        {
            return BadRequest("Name and Protocol are required.");
        }

        entity.Name = request.Name;
        entity.Protocol = request.Protocol;
        entity.Enabled = request.Enabled;
        entity.UpdateTime = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.app.updated",
                $"App {entity.Code} updated."),
            cancellationToken);

        return Ok(entity);
    }

    [HttpDelete("{id}")]
    [RequirePermission("app.write")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = await dbContext.Apps
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsDeleted = true;
        entity.Enabled = false;
        entity.UpdateTime = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.app.deleted",
                $"App {entity.Code} deleted."),
            cancellationToken);

        return NoContent();
    }
}
