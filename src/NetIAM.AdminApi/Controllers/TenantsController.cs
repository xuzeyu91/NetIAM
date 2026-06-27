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
}
