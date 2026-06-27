using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Identity;
using NetIAM.Infrastructure.Persistence;

namespace NetIAM.Infrastructure.Services;

public sealed record PermissionDefinition(
    string Code,
    string Name,
    string Resource,
    string Action,
    string? Description = null);

public interface IRbacService
{
    Task<IReadOnlyCollection<PermissionEntity>> ListPermissionsAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<PermissionEntity> EnsurePermissionAsync(string tenantId, PermissionDefinition definition, CancellationToken cancellationToken = default);

    Task AssignPermissionToRoleAsync(string tenantId, string roleName, string permissionCode, CancellationToken cancellationToken = default);

    Task RemovePermissionFromRoleAsync(string tenantId, string roleName, string permissionCode, CancellationToken cancellationToken = default);

    Task GrantUserPermissionAsync(
        string tenantId,
        string userId,
        string permissionCode,
        PermissionGrantEffect effect,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<string>> GetEffectivePermissionCodesAsync(string tenantId, string userId, CancellationToken cancellationToken = default);

    Task<bool> UserHasPermissionAsync(string tenantId, string userId, string permissionCode, CancellationToken cancellationToken = default);
}

public sealed class RbacService(
    NetIamDbContext dbContext,
    UserManager<NetIamIdentityUser> userManager,
    RoleManager<IdentityRole> roleManager) : IRbacService
{
    public async Task<IReadOnlyCollection<PermissionEntity>> ListPermissionsAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Permissions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<PermissionEntity> EnsurePermissionAsync(
        string tenantId,
        PermissionDefinition definition,
        CancellationToken cancellationToken = default)
    {
        var permission = await dbContext.Permissions
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == definition.Code && !x.IsDeleted, cancellationToken);

        if (permission is null)
        {
            permission = new PermissionEntity
            {
                TenantId = tenantId,
                Code = definition.Code,
                Name = definition.Name,
                Resource = definition.Resource,
                Action = definition.Action,
                Description = definition.Description
            };
            dbContext.Permissions.Add(permission);
        }
        else
        {
            permission.Name = definition.Name;
            permission.Resource = definition.Resource;
            permission.Action = definition.Action;
            permission.Description = definition.Description;
            permission.UpdateTime = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return permission;
    }

    public async Task AssignPermissionToRoleAsync(
        string tenantId,
        string roleName,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        var role = await EnsureRoleAsync(roleName);
        var permission = await ResolvePermissionAsync(tenantId, permissionCode, cancellationToken);

        var exists = await dbContext.RolePermissions.AnyAsync(
            x => x.TenantId == tenantId && x.RoleId == role.Id && x.PermissionId == permission.Id && !x.IsDeleted,
            cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.RolePermissions.Add(new RolePermissionEntity
        {
            TenantId = tenantId,
            RoleId = role.Id,
            PermissionId = permission.Id
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemovePermissionFromRoleAsync(
        string tenantId,
        string roleName,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            return;
        }

        var permission = await dbContext.Permissions
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == permissionCode && !x.IsDeleted, cancellationToken);
        if (permission is null)
        {
            return;
        }

        var target = await dbContext.RolePermissions
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.RoleId == role.Id && x.PermissionId == permission.Id && !x.IsDeleted,
                cancellationToken);
        if (target is null)
        {
            return;
        }

        target.IsDeleted = true;
        target.UpdateTime = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task GrantUserPermissionAsync(
        string tenantId,
        string userId,
        string permissionCode,
        PermissionGrantEffect effect,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == userId && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException($"User not found: {userId}.");
        _ = user;

        var permission = await ResolvePermissionAsync(tenantId, permissionCode, cancellationToken);

        var existing = await dbContext.UserPermissionGrants
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                     && x.UserId == userId
                     && x.PermissionId == permission.Id
                     && x.Effect == effect
                     && !x.IsDeleted,
                cancellationToken);
        if (existing is not null)
        {
            return;
        }

        dbContext.UserPermissionGrants.Add(new UserPermissionGrantEntity
        {
            TenantId = tenantId,
            UserId = userId,
            PermissionId = permission.Id,
            Effect = effect
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetEffectivePermissionCodesAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == userId && !x.IsDeleted, cancellationToken);
        if (user is null)
        {
            return [];
        }

        var roleIds = await dbContext.Set<IdentityUserRole<string>>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.RoleId)
            .ToListAsync(cancellationToken);

        var rolePermissionIds = await dbContext.RolePermissions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && roleIds.Contains(x.RoleId) && !x.IsDeleted)
            .Select(x => x.PermissionId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var userGrants = await dbContext.UserPermissionGrants
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.UserId == userId && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var denyIds = userGrants
            .Where(x => x.Effect == PermissionGrantEffect.Deny)
            .Select(x => x.PermissionId)
            .ToHashSet();
        var allowIds = userGrants
            .Where(x => x.Effect == PermissionGrantEffect.Allow)
            .Select(x => x.PermissionId);

        var effectivePermissionIds = rolePermissionIds
            .Concat(allowIds)
            .Distinct()
            .Where(x => !denyIds.Contains(x))
            .ToList();

        if (effectivePermissionIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Permissions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && effectivePermissionIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => x.Code)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> UserHasPermissionAsync(
        string tenantId,
        string userId,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        var permission = await dbContext.Permissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == permissionCode && !x.IsDeleted, cancellationToken);
        if (permission is null)
        {
            return false;
        }

        var userGrant = await dbContext.UserPermissionGrants
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.UserId == userId && x.PermissionId == permission.Id && !x.IsDeleted)
            .OrderByDescending(x => x.UpdateTime)
            .ThenByDescending(x => x.CreateTime)
            .FirstOrDefaultAsync(cancellationToken);

        if (userGrant is not null)
        {
            return userGrant.Effect == PermissionGrantEffect.Allow;
        }

        var roleIds = await dbContext.Set<IdentityUserRole<string>>()
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.RoleId)
            .ToListAsync(cancellationToken);
        if (roleIds.Count == 0)
        {
            return false;
        }

        return await dbContext.RolePermissions
            .AsNoTracking()
            .AnyAsync(
                x => x.TenantId == tenantId && x.PermissionId == permission.Id && roleIds.Contains(x.RoleId) && !x.IsDeleted,
                cancellationToken);
    }

    private async Task<IdentityRole> EnsureRoleAsync(string roleName)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        if (role is not null)
        {
            return role;
        }

        var createResult = await roleManager.CreateAsync(new IdentityRole(roleName));
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create role {roleName}: {string.Join("; ", createResult.Errors.Select(x => x.Description))}");
        }

        role = await roleManager.FindByNameAsync(roleName)
            ?? throw new InvalidOperationException($"Role creation not persisted: {roleName}.");
        return role;
    }

    private async Task<PermissionEntity> ResolvePermissionAsync(string tenantId, string permissionCode, CancellationToken cancellationToken)
    {
        return await dbContext.Permissions
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == permissionCode && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException($"Permission not found: {permissionCode}.");
    }
}
