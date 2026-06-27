using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Identity;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/security")]
public sealed class SecurityController(
    NetIamDbContext dbContext,
    UserManager<NetIamIdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ITenantContextAccessor tenantContextAccessor,
    ISystemSettingStore systemSettingStore,
    IAuditService auditService) : ControllerBase
{
    private const string AdministratorRoleName = "Administrator";
    private const string BasicSettingKey = "security.basic";
    private const string PasswordPolicySettingKey = "security.password-policy";
    private const string DefensePolicySettingKey = "security.defense-policy";

    public sealed record SecurityBasicSettings(
        int SessionTimeoutMinutes = 480,
        int SessionMaximum = 5,
        bool RememberMeEnabled = true,
        int RememberMeDays = 14,
        bool CaptchaEnabled = false,
        int CaptchaTtlSeconds = 300,
        bool AllowMultipleSessions = true);

    public sealed record PasswordPolicySettings(
        int RequiredLength = 12,
        bool RequireDigit = true,
        bool RequireLowercase = true,
        bool RequireUppercase = true,
        bool RequireNonAlphanumeric = true,
        int PasswordHistoryCount = 5,
        int PasswordMaxAgeDays = 90,
        bool EnableWeakPasswordCheck = true);

    public sealed record DefensePolicySettings(
        int MaxFailedAttempts = 5,
        int LockoutMinutes = 15,
        int AutoUnlockMinutes = 15,
        bool EnableIpRateLimit = true,
        bool EnableRiskAudit = true);

    [HttpGet("basic")]
    [RequirePermission("security.read")]
    public async Task<IActionResult> GetBasic(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var settings = await systemSettingStore.GetAsync(tenantId, BasicSettingKey, new SecurityBasicSettings(), cancellationToken);
        return Ok(settings);
    }

    [HttpPut("basic")]
    [RequirePermission("security.write")]
    public async Task<IActionResult> UpdateBasic([FromBody] SecurityBasicSettings request, CancellationToken cancellationToken)
    {
        var validationError = ValidateBasic(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        await systemSettingStore.SetAsync(tenantId, BasicSettingKey, request, cancellationToken);
        await auditService.WriteAsync(
            new AuditWriteRequest(tenantId, "admin.security.basic.updated", "Security basic settings updated."),
            cancellationToken);
        return Ok(request);
    }

    [HttpGet("password-policy")]
    [RequirePermission("security.read")]
    public async Task<IActionResult> GetPasswordPolicy(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var settings = await systemSettingStore.GetAsync(tenantId, PasswordPolicySettingKey, new PasswordPolicySettings(), cancellationToken);
        return Ok(settings);
    }

    [HttpPut("password-policy")]
    [RequirePermission("security.write")]
    public async Task<IActionResult> UpdatePasswordPolicy([FromBody] PasswordPolicySettings request, CancellationToken cancellationToken)
    {
        var validationError = ValidatePasswordPolicy(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        await systemSettingStore.SetAsync(tenantId, PasswordPolicySettingKey, request, cancellationToken);
        await auditService.WriteAsync(
            new AuditWriteRequest(tenantId, "admin.security.password-policy.updated", "Password policy updated."),
            cancellationToken);
        return Ok(request);
    }

    [HttpGet("defense-policy")]
    [RequirePermission("security.read")]
    public async Task<IActionResult> GetDefensePolicy(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var settings = await systemSettingStore.GetAsync(tenantId, DefensePolicySettingKey, new DefensePolicySettings(), cancellationToken);
        return Ok(settings);
    }

    [HttpPut("defense-policy")]
    [RequirePermission("security.write")]
    public async Task<IActionResult> UpdateDefensePolicy([FromBody] DefensePolicySettings request, CancellationToken cancellationToken)
    {
        var validationError = ValidateDefensePolicy(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        await systemSettingStore.SetAsync(tenantId, DefensePolicySettingKey, request, cancellationToken);
        await auditService.WriteAsync(
            new AuditWriteRequest(tenantId, "admin.security.defense-policy.updated", "Defense policy updated."),
            cancellationToken);
        return Ok(request);
    }

    [HttpGet("administrators")]
    [RequirePermission("security.read")]
    public async Task<IActionResult> ListAdministrators(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var adminRole = await EnsureAdministratorRoleAsync(cancellationToken);
        var adminUserIds = await dbContext.Set<IdentityUserRole<string>>()
            .Where(x => x.RoleId == adminRole.Id)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        if (adminUserIds.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var administrators = await userManager.Users
            .Where(x => x.TenantId == tenantId && adminUserIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                x.UserName,
                x.DisplayName,
                x.Email,
                x.PhoneNumber,
                x.CreateTime,
                x.UpdateTime
            })
            .OrderBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        return Ok(administrators);
    }

    [HttpPost("administrators/{userId}")]
    [RequirePermission("security.write")]
    public async Task<IActionResult> AddAdministrator(string userId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var user = await userManager.Users
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == userId && !x.IsDeleted, cancellationToken);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        var isInRole = await userManager.IsInRoleAsync(user, AdministratorRoleName);
        if (!isInRole)
        {
            var result = await userManager.AddToRoleAsync(user, AdministratorRoleName);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors.Select(x => x.Description));
            }
        }

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.security.administrator.added",
                $"User {user.UserName} added as administrator.",
                TargetJson: $$"""{"userId":"{{user.Id}}"}"""),
            cancellationToken);

        return Ok(new { user.Id, user.UserName, role = AdministratorRoleName });
    }

    [HttpDelete("administrators/{userId}")]
    [RequirePermission("security.write")]
    public async Task<IActionResult> RemoveAdministrator(string userId, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var user = await userManager.Users
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == userId && !x.IsDeleted, cancellationToken);
        if (user is null)
        {
            return NotFound("User not found.");
        }

        var isInRole = await userManager.IsInRoleAsync(user, AdministratorRoleName);
        if (!isInRole)
        {
            return NoContent();
        }

        var adminCount = await CountTenantAdministratorsAsync(tenantId, cancellationToken);
        if (adminCount <= 1)
        {
            return BadRequest("At least one administrator must remain.");
        }

        var result = await userManager.RemoveFromRoleAsync(user, AdministratorRoleName);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(x => x.Description));
        }

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.security.administrator.removed",
                $"User {user.UserName} removed from administrator role.",
                TargetJson: $$"""{"userId":"{{user.Id}}"}"""),
            cancellationToken);

        return NoContent();
    }

    private static string? ValidateBasic(SecurityBasicSettings request)
    {
        if (request.SessionTimeoutMinutes is < 5 or > 43200)
        {
            return "SessionTimeoutMinutes must be between 5 and 43200.";
        }

        if (request.SessionMaximum is < 1 or > 100)
        {
            return "SessionMaximum must be between 1 and 100.";
        }

        if (request.RememberMeDays is < 1 or > 365)
        {
            return "RememberMeDays must be between 1 and 365.";
        }

        if (request.CaptchaTtlSeconds is < 30 or > 3600)
        {
            return "CaptchaTtlSeconds must be between 30 and 3600.";
        }

        return null;
    }

    private static string? ValidatePasswordPolicy(PasswordPolicySettings request)
    {
        if (request.RequiredLength is < 8 or > 128)
        {
            return "RequiredLength must be between 8 and 128.";
        }

        if (request.PasswordHistoryCount is < 0 or > 24)
        {
            return "PasswordHistoryCount must be between 0 and 24.";
        }

        if (request.PasswordMaxAgeDays is < 0 or > 3650)
        {
            return "PasswordMaxAgeDays must be between 0 and 3650.";
        }

        return null;
    }

    private static string? ValidateDefensePolicy(DefensePolicySettings request)
    {
        if (request.MaxFailedAttempts is < 1 or > 20)
        {
            return "MaxFailedAttempts must be between 1 and 20.";
        }

        if (request.LockoutMinutes is < 1 or > 1440)
        {
            return "LockoutMinutes must be between 1 and 1440.";
        }

        if (request.AutoUnlockMinutes is < 1 or > 1440)
        {
            return "AutoUnlockMinutes must be between 1 and 1440.";
        }

        return null;
    }

    private async Task<IdentityRole> EnsureAdministratorRoleAsync(CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByNameAsync(AdministratorRoleName);
        if (role is not null)
        {
            return role;
        }

        var createResult = await roleManager.CreateAsync(new IdentityRole(AdministratorRoleName));
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to ensure role {AdministratorRoleName}: {string.Join("; ", createResult.Errors.Select(x => x.Description))}");
        }

        role = await roleManager.FindByNameAsync(AdministratorRoleName)
            ?? throw new InvalidOperationException($"Role not found after create: {AdministratorRoleName}");
        return role;
    }

    private async Task<int> CountTenantAdministratorsAsync(string tenantId, CancellationToken cancellationToken)
    {
        var adminRole = await EnsureAdministratorRoleAsync(cancellationToken);
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
