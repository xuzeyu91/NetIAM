using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Identity;
using NetIAM.Infrastructure.Persistence;

namespace NetIAM.Infrastructure.Services;

public interface INetIamDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

public sealed class NetIamDataSeeder(
    NetIamDbContext dbContext,
    UserManager<NetIamIdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IRbacService rbacService,
    ILogger<NetIamDataSeeder> logger) : INetIamDataSeeder
{
    private const string DefaultTenantId = "tenant-default";
    private const string DefaultTenantIdentifier = "default";
    private const string DefaultAdminUsername = "admin";
    private const string DefaultAdminPassword = "NetIAM.Admin#2026";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.Identifier == DefaultTenantIdentifier, cancellationToken);
        if (tenant is null)
        {
            tenant = new TenantEntity
            {
                Id = DefaultTenantId,
                Identifier = DefaultTenantIdentifier,
                Name = "Default Tenant",
                IsActive = true
            };
            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        const string administratorRoleName = "Administrator";
        if (!await roleManager.RoleExistsAsync(administratorRoleName))
        {
            await roleManager.CreateAsync(new IdentityRole(administratorRoleName));
        }

        var admin = await userManager.Users
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.UserName == DefaultAdminUsername, cancellationToken);

        if (admin is null)
        {
            admin = new NetIamIdentityUser
            {
                Id = "user-admin-default",
                TenantId = tenant.Id,
                UserName = DefaultAdminUsername,
                DisplayName = "System Administrator",
                Email = "admin@netiam.local",
                EmailConfirmed = true,
                DataOrigin = DataOriginType.Local
            };

            var createResult = await userManager.CreateAsync(admin, DefaultAdminPassword);
            if (!createResult.Succeeded)
            {
                var errorMessages = string.Join("; ", createResult.Errors.Select(x => x.Description));
                throw new InvalidOperationException($"Failed to seed admin user: {errorMessages}");
            }

            logger.LogInformation("Default admin user created. Username: {Username}", DefaultAdminUsername);
        }

        if (!await userManager.IsInRoleAsync(admin, administratorRoleName))
        {
            await userManager.AddToRoleAsync(admin, administratorRoleName);
        }

        var permissions = GetDefaultPermissions();
        foreach (var permission in permissions)
        {
            await rbacService.EnsurePermissionAsync(tenant.Id, permission, cancellationToken);
            await rbacService.AssignPermissionToRoleAsync(tenant.Id, administratorRoleName, permission.Code, cancellationToken);
        }
    }

    private static IReadOnlyCollection<PermissionDefinition> GetDefaultPermissions()
    {
        return
        [
            new("tenant.read", "Read Tenants", "tenant", "read"),
            new("tenant.write", "Manage Tenants", "tenant", "write"),
            new("user.read", "Read Users", "user", "read"),
            new("user.write", "Manage Users", "user", "write"),
            new("organization.read", "Read Organizations", "organization", "read"),
            new("organization.write", "Manage Organizations", "organization", "write"),
            new("group.read", "Read User Groups", "user_group", "read"),
            new("group.write", "Manage User Groups", "user_group", "write"),
            new("provider.read", "Read Identity Providers", "identity_provider", "read"),
            new("provider.write", "Manage Identity Providers", "identity_provider", "write"),
            new("source.read", "Read Identity Sources", "identity_source", "read"),
            new("source.write", "Manage Identity Sources", "identity_source", "write"),
            new("app.read", "Read Applications", "application", "read"),
            new("app.write", "Manage Applications", "application", "write"),
            new("access-policy.read", "Read App Access Policies", "app_access_policy", "read"),
            new("access-policy.write", "Manage App Access Policies", "app_access_policy", "write"),
            new("audit.read", "Read Audit Events", "audit", "read"),
            new("rbac.read", "Read RBAC", "rbac", "read"),
            new("rbac.write", "Manage RBAC", "rbac", "write"),
            new("scim.read", "Read SCIM", "scim", "read"),
            new("scim.write", "Manage SCIM", "scim", "write"),
            new("saml.read", "Read SAML Config", "saml", "read"),
            new("saml.write", "Manage SAML Config", "saml", "write")
        ];
    }
}
