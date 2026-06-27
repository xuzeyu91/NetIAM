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

        if (!await roleManager.RoleExistsAsync("Administrator"))
        {
            await roleManager.CreateAsync(new IdentityRole("Administrator"));
        }

        var admin = await userManager.Users
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.UserName == DefaultAdminUsername, cancellationToken);

        if (admin is not null)
        {
            return;
        }

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

        await userManager.AddToRoleAsync(admin, "Administrator");
        logger.LogInformation("Default admin user created. Username: {Username}", DefaultAdminUsername);
    }
}
