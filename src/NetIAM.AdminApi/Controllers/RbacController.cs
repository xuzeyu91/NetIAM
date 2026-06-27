using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/rbac")]
public sealed class RbacController(
    IRbacService rbacService,
    RoleManager<IdentityRole> roleManager,
    ITenantContextAccessor tenantContextAccessor,
    IAuditService auditService) : ControllerBase
{
    public sealed record CreatePermissionRequest(
        string Code,
        string Name,
        string Resource,
        string Action,
        string? Description = null);

    public sealed record UserGrantRequest(string PermissionCode, PermissionGrantEffect Effect);

    [HttpGet("permissions")]
    [RequirePermission("rbac.read")]
    public async Task<IActionResult> ListPermissions(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var permissions = await rbacService.ListPermissionsAsync(tenantId, cancellationToken);
        return Ok(permissions);
    }

    [HttpPost("permissions")]
    [RequirePermission("rbac.write")]
    public async Task<IActionResult> CreatePermission([FromBody] CreatePermissionRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var permission = await rbacService.EnsurePermissionAsync(
            tenantId,
            new PermissionDefinition(request.Code, request.Name, request.Resource, request.Action, request.Description),
            cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(tenantId, "admin.rbac.permission.created", $"Permission {request.Code} upserted."),
            cancellationToken);

        return Ok(permission);
    }

    [HttpGet("roles")]
    [RequirePermission("rbac.read")]
    public IActionResult ListRoles()
    {
        var roles = roleManager.Roles
            .Select(x => new { x.Id, x.Name })
            .OrderBy(x => x.Name)
            .ToList();
        return Ok(roles);
    }

    [HttpPost("roles/{roleName}/permissions/{permissionCode}")]
    [RequirePermission("rbac.write")]
    public async Task<IActionResult> AssignRolePermission(
        string roleName,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        await rbacService.AssignPermissionToRoleAsync(tenantId, roleName, permissionCode, cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.rbac.role-permission.assigned",
                $"Role {roleName} assigned permission {permissionCode}."),
            cancellationToken);

        return Ok(new { roleName, permissionCode });
    }

    [HttpDelete("roles/{roleName}/permissions/{permissionCode}")]
    [RequirePermission("rbac.write")]
    public async Task<IActionResult> RemoveRolePermission(
        string roleName,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        await rbacService.RemovePermissionFromRoleAsync(tenantId, roleName, permissionCode, cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.rbac.role-permission.removed",
                $"Role {roleName} removed permission {permissionCode}."),
            cancellationToken);

        return NoContent();
    }

    [HttpGet("users/{userId}/permissions")]
    [RequirePermission("rbac.read")]
    public async Task<IActionResult> GetUserPermissions(string userId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var permissions = await rbacService.GetEffectivePermissionCodesAsync(tenantId, userId, cancellationToken);
        return Ok(new { userId, permissions });
    }

    [HttpPost("users/{userId}/grants")]
    [RequirePermission("rbac.write")]
    public async Task<IActionResult> GrantUserPermission(
        string userId,
        [FromBody] UserGrantRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        await rbacService.GrantUserPermissionAsync(tenantId, userId, request.PermissionCode, request.Effect, cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.rbac.user-grant.created",
                $"User {userId} grant {request.Effect} on {request.PermissionCode}."),
            cancellationToken);

        return Ok(new { userId, request.PermissionCode, effect = request.Effect.ToString() });
    }
}
