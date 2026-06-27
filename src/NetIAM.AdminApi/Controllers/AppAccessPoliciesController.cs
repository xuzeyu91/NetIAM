using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/app-access-policies")]
public sealed class AppAccessPoliciesController(
    NetIamDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    IAuditService auditService) : ControllerBase
{
    public sealed record CreateAppAccessPolicyRequest(
        string AppId,
        SubjectType SubjectType,
        string SubjectId,
        bool AllowAccess = true);

    public sealed record UpdateAppAccessPolicyRequest(
        string AppId,
        SubjectType SubjectType,
        string SubjectId,
        bool AllowAccess);

    [HttpGet]
    [RequirePermission("access-policy.read")]
    public async Task<IActionResult> List([FromQuery] string? appId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var query = dbContext.AppAccessPolicies
            .Where(x => x.TenantId == tenantId && !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(appId))
        {
            query = query.Where(x => x.AppId == appId);
        }

        var policies = await query
            .OrderByDescending(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        if (policies.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var appIds = policies.Select(x => x.AppId).Distinct().ToList();
        var apps = await dbContext.Apps
            .Where(x => x.TenantId == tenantId && appIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new { x.Id, x.Code, x.Name })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var userIds = policies.Where(x => x.SubjectType == SubjectType.User).Select(x => x.SubjectId).Distinct().ToList();
        var users = userIds.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await dbContext.Users
                .Where(x => x.TenantId == tenantId && userIds.Contains(x.Id) && !x.IsDeleted)
                .ToDictionaryAsync(x => x.Id, x => string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName, cancellationToken);

        var groupIds = policies.Where(x => x.SubjectType == SubjectType.Group).Select(x => x.SubjectId).Distinct().ToList();
        var groups = groupIds.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await dbContext.UserGroups
                .Where(x => x.TenantId == tenantId && groupIds.Contains(x.Id) && !x.IsDeleted)
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var organizationIds = policies.Where(x => x.SubjectType == SubjectType.Organization).Select(x => x.SubjectId).Distinct().ToList();
        var organizations = organizationIds.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await dbContext.Organizations
                .Where(x => x.TenantId == tenantId && organizationIds.Contains(x.Id) && !x.IsDeleted)
                .ToDictionaryAsync(x => x.Id, x => x.DisplayPath, cancellationToken);

        return Ok(policies.Select(x => new
        {
            x.Id,
            x.AppId,
            appCode = apps.GetValueOrDefault(x.AppId)?.Code,
            appName = apps.GetValueOrDefault(x.AppId)?.Name,
            x.SubjectType,
            x.SubjectId,
            subjectName = ResolveSubjectName(x, users, groups, organizations),
            x.AllowAccess,
            x.CreateTime,
            x.UpdateTime
        }));
    }

    [HttpPost]
    [RequirePermission("access-policy.write")]
    public async Task<IActionResult> Create([FromBody] CreateAppAccessPolicyRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var validationError = await ValidateRequestAsync(tenantId, request.AppId, request.SubjectType, request.SubjectId, cancellationToken);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var duplicate = await dbContext.AppAccessPolicies.AnyAsync(
            x => x.TenantId == tenantId
                 && x.AppId == request.AppId
                 && x.SubjectType == request.SubjectType
                 && x.SubjectId == request.SubjectId
                 && !x.IsDeleted,
            cancellationToken);
        if (duplicate)
        {
            return Conflict("Access policy already exists for this app and subject.");
        }

        var entity = new AppAccessPolicyEntity
        {
            TenantId = tenantId,
            AppId = request.AppId,
            SubjectType = request.SubjectType,
            SubjectId = request.SubjectId.Trim(),
            AllowAccess = request.AllowAccess
        };
        dbContext.AppAccessPolicies.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.app-access-policy.created",
                $"App access policy created for app {request.AppId}.",
                TargetJson: $$"""{"policyId":"{{entity.Id}}"}"""),
            cancellationToken);

        return Ok(entity);
    }

    [HttpPut("{id}")]
    [RequirePermission("access-policy.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateAppAccessPolicyRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = await dbContext.AppAccessPolicies
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var validationError = await ValidateRequestAsync(tenantId, request.AppId, request.SubjectType, request.SubjectId, cancellationToken);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var duplicate = await dbContext.AppAccessPolicies.AnyAsync(
            x => x.TenantId == tenantId
                 && x.Id != id
                 && x.AppId == request.AppId
                 && x.SubjectType == request.SubjectType
                 && x.SubjectId == request.SubjectId
                 && !x.IsDeleted,
            cancellationToken);
        if (duplicate)
        {
            return Conflict("Access policy already exists for this app and subject.");
        }

        entity.AppId = request.AppId;
        entity.SubjectType = request.SubjectType;
        entity.SubjectId = request.SubjectId.Trim();
        entity.AllowAccess = request.AllowAccess;
        entity.UpdateTime = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.app-access-policy.updated",
                $"App access policy {id} updated.",
                TargetJson: $$"""{"policyId":"{{entity.Id}}"}"""),
            cancellationToken);

        return Ok(entity);
    }

    [HttpDelete("{id}")]
    [RequirePermission("access-policy.write")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = await dbContext.AppAccessPolicies
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsDeleted = true;
        entity.UpdateTime = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.app-access-policy.deleted",
                $"App access policy {id} deleted.",
                TargetJson: $$"""{"policyId":"{{entity.Id}}"}"""),
            cancellationToken);

        return NoContent();
    }

    private async Task<string?> ValidateRequestAsync(
        string tenantId,
        string appId,
        SubjectType subjectType,
        string subjectId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return "AppId is required.";
        }

        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return "SubjectId is required.";
        }

        var appExists = await dbContext.Apps.AnyAsync(
            x => x.TenantId == tenantId && x.Id == appId && !x.IsDeleted,
            cancellationToken);
        if (!appExists)
        {
            return $"Application not found: {appId}.";
        }

        var normalizedSubjectId = subjectId.Trim();
        var subjectExists = subjectType switch
        {
            SubjectType.User => await dbContext.Users.AnyAsync(
                x => x.TenantId == tenantId && x.Id == normalizedSubjectId && !x.IsDeleted,
                cancellationToken),
            SubjectType.Group => await dbContext.UserGroups.AnyAsync(
                x => x.TenantId == tenantId && x.Id == normalizedSubjectId && !x.IsDeleted,
                cancellationToken),
            SubjectType.Organization => await dbContext.Organizations.AnyAsync(
                x => x.TenantId == tenantId && x.Id == normalizedSubjectId && !x.IsDeleted,
                cancellationToken),
            _ => false
        };
        if (!subjectExists)
        {
            return $"Subject not found for {subjectType}: {normalizedSubjectId}.";
        }

        return null;
    }

    private static string? ResolveSubjectName(
        AppAccessPolicyEntity policy,
        IReadOnlyDictionary<string, string> users,
        IReadOnlyDictionary<string, string> groups,
        IReadOnlyDictionary<string, string> organizations)
    {
        return policy.SubjectType switch
        {
            SubjectType.User => users.GetValueOrDefault(policy.SubjectId),
            SubjectType.Group => groups.GetValueOrDefault(policy.SubjectId),
            SubjectType.Organization => organizations.GetValueOrDefault(policy.SubjectId),
            _ => null
        };
    }
}
