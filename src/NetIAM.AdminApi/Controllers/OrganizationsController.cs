using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/organizations")]
public sealed class OrganizationsController(
    NetIamDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    IAuditService auditService) : ControllerBase
{
    public sealed record CreateOrganizationRequest(
        string Name,
        string Code,
        string? ParentId = null);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var organizations = await dbContext.Organizations
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.Path)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return Ok(organizations);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var parent = string.IsNullOrWhiteSpace(request.ParentId)
            ? null
            : await dbContext.Organizations
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.ParentId && !x.IsDeleted, cancellationToken);

        var entity = new OrganizationEntity
        {
            TenantId = tenantId,
            Name = request.Name,
            Code = request.Code,
            ParentId = parent?.Id,
            Path = parent is null ? "/" : $"{parent.Path}{parent.Id}/",
            DisplayPath = parent is null ? $"/{request.Name}" : $"{parent.DisplayPath}/{request.Name}",
            DataOrigin = DataOriginType.Local
        };
        dbContext.Organizations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.organization.created",
                $"Organization {request.Name} created.",
                TargetJson: $$"""{"organizationId":"{{entity.Id}}"}"""),
            cancellationToken);

        return Ok(entity);
    }
}
