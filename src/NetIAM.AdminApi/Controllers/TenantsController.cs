using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Entities;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/tenants")]
public sealed class TenantsController(
    NetIamDbContext dbContext,
    IAuditService auditService) : ControllerBase
{
    public sealed record CreateTenantRequest(string Identifier, string Name, string? DefaultDomain);
    public sealed record UpdateTenantRequest(string Identifier, string Name, string? DefaultDomain, bool IsActive);

    [HttpGet]
    [RequirePermission("tenant.read")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenants = await dbContext.Tenants
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Identifier)
            .ToListAsync(cancellationToken);
        return Ok(tenants);
    }

    [HttpPost]
    [RequirePermission("tenant.write")]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Identifier and Name are required.");
        }

        var exists = await dbContext.Tenants.AnyAsync(x => x.Identifier == request.Identifier && !x.IsDeleted, cancellationToken);
        if (exists)
        {
            return Conflict($"Tenant identifier '{request.Identifier}' already exists.");
        }

        var tenant = new TenantEntity
        {
            Identifier = request.Identifier,
            Name = request.Name,
            DefaultDomain = request.DefaultDomain,
            IsActive = true
        };
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenant.Id,
                "admin.tenant.created",
                $"Tenant {request.Identifier} created.",
                TargetJson: $$"""{"tenantId":"{{tenant.Id}}"}"""),
            cancellationToken);

        return Ok(tenant);
    }

    [HttpPut("{id}")]
    [RequirePermission("tenant.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Identifier and Name are required.");
        }

        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        var identifierExists = await dbContext.Tenants.AnyAsync(
            x => x.Id != id && x.Identifier == request.Identifier && !x.IsDeleted,
            cancellationToken);
        if (identifierExists)
        {
            return Conflict($"Tenant identifier '{request.Identifier}' already exists.");
        }

        tenant.Identifier = request.Identifier;
        tenant.Name = request.Name;
        tenant.DefaultDomain = request.DefaultDomain;
        tenant.IsActive = request.IsActive;
        tenant.UpdateTime = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenant.Id,
                "admin.tenant.updated",
                $"Tenant {request.Identifier} updated.",
                TargetJson: $$"""{"tenantId":"{{tenant.Id}}"}"""),
            cancellationToken);

        return Ok(tenant);
    }

    [HttpDelete("{id}")]
    [RequirePermission("tenant.write")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        tenant.IsDeleted = true;
        tenant.IsActive = false;
        tenant.UpdateTime = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenant.Id,
                "admin.tenant.deleted",
                $"Tenant {tenant.Identifier} deleted.",
                TargetJson: $$"""{"tenantId":"{{tenant.Id}}"}"""),
            cancellationToken);

        return NoContent();
    }
}
