using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Identity;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/rbac")]
public sealed class RbacController(
    IRbacService rbacService,
    RoleManager<IdentityRole> roleManager,
    UserManager<NetIamIdentityUser> userManager,
    NetIamDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    IAuditService auditService) : ControllerBase
{
    public sealed record CreatePermissionRequest(
        string Code,
        string Name,
        string Resource,
        string Action,
        string? Description = null);

    public sealed record CreateRoleRequest(string Name);

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

    [HttpPost("roles")]
    [RequirePermission("rbac.write")]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Role name is required.");
        }

        if (await roleManager.RoleExistsAsync(request.Name))
        {
            return Conflict($"Role already exists: {request.Name}.");
        }

        var result = await roleManager.CreateAsync(new IdentityRole(request.Name));
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(x => x.Description));
        }

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.rbac.role.created",
                $"Role {request.Name} created."),
            cancellationToken);

        var role = await roleManager.FindByNameAsync(request.Name);
        return Ok(new { role?.Id, role?.Name });
    }

    [HttpDelete("roles/{roleName}")]
    [RequirePermission("rbac.write")]
    public async Task<IActionResult> DeleteRole(string roleName, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        if (string.Equals(roleName, "Administrator", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Administrator role cannot be deleted.");
        }

        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            return NotFound();
        }

        var rolePermissions = await dbContext.RolePermissions
            .Where(x => x.TenantId == tenantId && x.RoleId == role.Id && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var rolePermission in rolePermissions)
        {
            rolePermission.IsDeleted = true;
            rolePermission.UpdateTime = DateTimeOffset.UtcNow;
        }

        var result = await roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(x => x.Description));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.rbac.role.deleted",
                $"Role {roleName} deleted."),
            cancellationToken);

        return NoContent();
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

    [HttpGet("users/{userId}/roles")]
    [RequirePermission("rbac.read")]
    public async Task<IActionResult> GetUserRoles(string userId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var user = await userManager.Users
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == userId && !x.IsDeleted, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await userManager.GetRolesAsync(user);
        return Ok(new { userId, roles });
    }

    [HttpPost("users/{userId}/roles/{roleName}")]
    [RequirePermission("rbac.write")]
    public async Task<IActionResult> AssignUserRole(
        string userId,
        string roleName,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var user = await userManager.Users
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == userId && !x.IsDeleted, cancellationToken);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        if (!await roleManager.RoleExistsAsync(roleName))
        {
            return NotFound($"Role not found: {roleName}.");
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            var assignResult = await userManager.AddToRoleAsync(user, roleName);
            if (!assignResult.Succeeded)
            {
                return BadRequest(assignResult.Errors.Select(x => x.Description));
            }
        }

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.rbac.user-role.assigned",
                $"User {userId} assigned role {roleName}."),
            cancellationToken);

        return Ok(new { userId, roleName });
    }

    [HttpDelete("users/{userId}/roles/{roleName}")]
    [RequirePermission("rbac.write")]
    public async Task<IActionResult> RemoveUserRole(
        string userId,
        string roleName,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var user = await userManager.Users
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == userId && !x.IsDeleted, cancellationToken);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        if (!await roleManager.RoleExistsAsync(roleName))
        {
            return NotFound($"Role not found: {roleName}.");
        }

        var isInRole = await userManager.IsInRoleAsync(user, roleName);
        if (isInRole && string.Equals(roleName, "Administrator", StringComparison.OrdinalIgnoreCase))
        {
            var tenantAdminCount = await CountTenantAdministratorsAsync(tenantId, cancellationToken);
            if (tenantAdminCount <= 1)
            {
                return BadRequest("At least one administrator must remain.");
            }
        }

        if (isInRole)
        {
            var removeResult = await userManager.RemoveFromRoleAsync(user, roleName);
            if (!removeResult.Succeeded)
            {
                return BadRequest(removeResult.Errors.Select(x => x.Description));
            }
        }

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.rbac.user-role.removed",
                $"User {userId} removed role {roleName}."),
            cancellationToken);

        return NoContent();
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

    [HttpGet("users/{userId}/grants")]
    [RequirePermission("rbac.read")]
    public async Task<IActionResult> ListUserGrants(string userId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var userExists = await userManager.Users.AnyAsync(
            x => x.TenantId == tenantId && x.Id == userId && !x.IsDeleted,
            cancellationToken);
        if (!userExists)
        {
            return NotFound("User not found.");
        }

        var grants = await dbContext.UserPermissionGrants
            .Where(x => x.TenantId == tenantId && x.UserId == userId && !x.IsDeleted)
            .Join(
                dbContext.Permissions.Where(x => x.TenantId == tenantId && !x.IsDeleted),
                grant => grant.PermissionId,
                permission => permission.Id,
                (grant, permission) => new
                {
                    grant.Id,
                    permission.Code,
                    grant.Effect,
                    grant.CreateTime,
                    grant.UpdateTime
                })
            .OrderByDescending(x => x.UpdateTime)
            .ThenByDescending(x => x.CreateTime)
            .ToListAsync(cancellationToken);

        return Ok(grants);
    }

    [HttpDelete("users/{userId}/grants/{permissionCode}")]
    [RequirePermission("rbac.write")]
    public async Task<IActionResult> RevokeUserPermissionGrant(
        string userId,
        string permissionCode,
        [FromQuery] PermissionGrantEffect? effect = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var userExists = await userManager.Users.AnyAsync(
            x => x.TenantId == tenantId && x.Id == userId && !x.IsDeleted,
            cancellationToken);
        if (!userExists)
        {
            return NotFound("User not found.");
        }

        var permission = await dbContext.Permissions
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == permissionCode && !x.IsDeleted, cancellationToken);
        if (permission is null)
        {
            return NotFound($"Permission not found: {permissionCode}.");
        }

        var query = dbContext.UserPermissionGrants
            .Where(x => x.TenantId == tenantId
                        && x.UserId == userId
                        && x.PermissionId == permission.Id
                        && !x.IsDeleted);
        if (effect.HasValue)
        {
            query = query.Where(x => x.Effect == effect.Value);
        }

        var targets = await query.ToListAsync(cancellationToken);
        if (targets.Count == 0)
        {
            return NotFound("No matching permission grant found.");
        }

        foreach (var target in targets)
        {
            target.IsDeleted = true;
            target.UpdateTime = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.rbac.user-grant.revoked",
                $"User {userId} grant revoked on {permissionCode}."),
            cancellationToken);

        return NoContent();
    }

    private async Task<int> CountTenantAdministratorsAsync(string tenantId, CancellationToken cancellationToken)
    {
        var adminRole = await roleManager.FindByNameAsync("Administrator");
        if (adminRole is null)
        {
            return 0;
        }

        var adminUserIds = await dbContext.Set<IdentityUserRole<string>>()
            .Where(x => x.RoleId == adminRole.Id)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);
        if (adminUserIds.Count == 0)
        {
            return 0;
        }

        return await userManager.Users.CountAsync(
            x => x.TenantId == tenantId && adminUserIds.Contains(x.Id) && !x.IsDeleted,
            cancellationToken);
    }
}
