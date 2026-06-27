using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Authorization;
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

    public sealed record UpdateOrganizationRequest(
        string Name,
        string Code,
        string? ParentId = null);

    [HttpGet]
    [RequirePermission("organization.read")]
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
    [RequirePermission("organization.write")]
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

    [HttpPut("{id}")]
    [RequirePermission("organization.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateOrganizationRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = await dbContext.Organizations
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest("Name and Code are required.");
        }

        if (!string.IsNullOrWhiteSpace(request.ParentId) && request.ParentId == id)
        {
            return BadRequest("Organization cannot be parent of itself.");
        }

        var parent = string.IsNullOrWhiteSpace(request.ParentId)
            ? null
            : await dbContext.Organizations
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.ParentId && !x.IsDeleted, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.ParentId) && parent is null)
        {
            return BadRequest($"Parent organization not found: {request.ParentId}.");
        }

        if (parent is not null && parent.Path.Contains($"/{id}/", StringComparison.Ordinal))
        {
            return BadRequest("Parent cannot be a child of current organization.");
        }

        var codeExists = await dbContext.Organizations.AnyAsync(
            x => x.TenantId == tenantId && x.Id != id && x.Code == request.Code && !x.IsDeleted,
            cancellationToken);
        if (codeExists)
        {
            return Conflict($"Organization code already exists: {request.Code}.");
        }

        entity.Name = request.Name;
        entity.Code = request.Code;
        entity.ParentId = parent?.Id;
        entity.UpdateTime = DateTimeOffset.UtcNow;

        await RebuildOrganizationPathsAsync(tenantId, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.organization.updated",
                $"Organization {entity.Name} updated.",
                TargetJson: $$"""{"organizationId":"{{entity.Id}}"}"""),
            cancellationToken);

        return Ok(entity);
    }

    [HttpDelete("{id}")]
    [RequirePermission("organization.write")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = await dbContext.Organizations
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var hasChildren = await dbContext.Organizations.AnyAsync(
            x => x.TenantId == tenantId && x.ParentId == id && !x.IsDeleted,
            cancellationToken);
        if (hasChildren)
        {
            return Conflict("Cannot delete organization with active child organizations.");
        }

        entity.IsDeleted = true;
        entity.UpdateTime = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.organization.deleted",
                $"Organization {entity.Name} deleted.",
                TargetJson: $$"""{"organizationId":"{{entity.Id}}"}"""),
            cancellationToken);

        return NoContent();
    }

    private async Task RebuildOrganizationPathsAsync(string tenantId, CancellationToken cancellationToken)
    {
        var organizations = await dbContext.Organizations
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        if (organizations.Count == 0)
        {
            return;
        }

        var byId = organizations.ToDictionary(x => x.Id, x => x);
        var childrenByParent = organizations
            .GroupBy(x => x.ParentId ?? string.Empty)
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Name, StringComparer.Ordinal).ToList());

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new HashSet<string>(StringComparer.Ordinal);

        void AssignNode(OrganizationEntity node, OrganizationEntity? parent)
        {
            if (!stack.Add(node.Id))
            {
                node.ParentId = null;
                node.Path = "/";
                node.DisplayPath = $"/{node.Name}";
                return;
            }

            if (visited.Contains(node.Id))
            {
                stack.Remove(node.Id);
                return;
            }

            node.Path = parent is null ? "/" : $"{parent.Path}{parent.Id}/";
            node.DisplayPath = parent is null ? $"/{node.Name}" : $"{parent.DisplayPath}/{node.Name}";
            visited.Add(node.Id);

            if (childrenByParent.TryGetValue(node.Id, out var children))
            {
                foreach (var child in children)
                {
                    AssignNode(child, node);
                }
            }

            stack.Remove(node.Id);
        }

        foreach (var organization in organizations)
        {
            if (organization.ParentId is null)
            {
                AssignNode(organization, null);
            }
        }

        foreach (var organization in organizations)
        {
            if (visited.Contains(organization.Id))
            {
                continue;
            }

            if (organization.ParentId is not null && byId.TryGetValue(organization.ParentId, out var parent))
            {
                AssignNode(organization, parent);
                continue;
            }

            AssignNode(organization, null);
        }
    }
}
