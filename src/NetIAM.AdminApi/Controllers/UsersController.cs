using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Infrastructure.Identity;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/users")]
public sealed class UsersController(
    UserManager<NetIamIdentityUser> userManager,
    ITenantContextAccessor tenantContextAccessor,
    IAuditService auditService) : ControllerBase
{
    public sealed record CreateUserRequest(
        string Username,
        string DisplayName,
        string Password,
        string? Email,
        string? PhoneNumber,
        string? ExternalId);

    public sealed record UpdateUserRequest(
        string DisplayName,
        string? Email,
        string? PhoneNumber,
        string? ExternalId);

    [HttpGet]
    [RequirePermission("user.read")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var users = await userManager.Users
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                x.UserName,
                x.DisplayName,
                x.Email,
                x.PhoneNumber,
                x.ExternalId,
                x.LockoutEnd,
                x.AccessFailedCount,
                x.CreateTime,
                x.UpdateTime
            })
            .OrderBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpPost]
    [RequirePermission("user.write")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var user = new NetIamIdentityUser
        {
            TenantId = tenantId,
            UserName = request.Username,
            DisplayName = request.DisplayName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            ExternalId = request.ExternalId,
            EmailConfirmed = false,
            CreateBy = User.Identity?.Name
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(x => x.Description));
        }

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.user.created",
                $"User {request.Username} created.",
                TargetJson: $$"""{"userId":"{{user.Id}}"}"""),
            cancellationToken);

        return Ok(new { user.Id, user.UserName });
    }

    [HttpPut("{id}")]
    [RequirePermission("user.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var user = await userManager.Users.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.DisplayName = request.DisplayName;
        user.Email = request.Email;
        user.PhoneNumber = request.PhoneNumber;
        user.ExternalId = request.ExternalId;
        user.UpdateTime = DateTimeOffset.UtcNow;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(x => x.Description));
        }

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.user.updated",
                $"User {user.UserName} updated.",
                TargetJson: $$"""{"userId":"{{user.Id}}"}"""),
            cancellationToken);

        return Ok(new { user.Id, user.UserName });
    }

    [HttpDelete("{id}")]
    [RequirePermission("user.write")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var user = await userManager.Users.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.IsDeleted = true;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.UpdateTime = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.user.deleted",
                $"User {user.UserName} marked as deleted.",
                TargetJson: $$"""{"userId":"{{user.Id}}"}"""),
            cancellationToken);

        return NoContent();
    }
}
