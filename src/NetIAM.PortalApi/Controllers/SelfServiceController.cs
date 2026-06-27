using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Identity;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

namespace NetIAM.PortalApi.Controllers;

[ApiController]
[Route("api/portal")]
public sealed class SelfServiceController(
    NetIamDbContext dbContext,
    UserManager<NetIamIdentityUser> userManager,
    ITenantContextAccessor tenantContextAccessor,
    ISessionRevocationService sessionRevocationService,
    ISessionTerminationService sessionTerminationService,
    IAuditService auditService) : ControllerBase
{
    public sealed record UpdateProfileRequest(string DisplayName, string? Email, string? PhoneNumber);

    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var current = await ResolveCurrentUserAsync(cancellationToken);
        if (current.Result is not null)
        {
            return current.Result;
        }

        var user = current.User!;
        return Ok(BuildUserProfile(user));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var current = await ResolveCurrentUserAsync(cancellationToken);
        if (current.Result is not null)
        {
            return current.Result;
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest("DisplayName is required.");
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        var user = current.User!;
        user.DisplayName = request.DisplayName.Trim();
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.UpdateTime = DateTimeOffset.UtcNow;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return BadRequest(updateResult.Errors.Select(x => x.Description));
        }

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "portal.account.profile.updated",
                $"User {user.UserName} updated profile.",
                ActorType: "user",
                ActorId: user.Id,
                RequestId: HttpContext.TraceIdentifier,
                SessionId: ResolveSessionId(),
                UserAgent: Request.Headers.UserAgent.ToString(),
                IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return Ok(BuildUserProfile(user));
    }

    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var current = await ResolveCurrentUserAsync(cancellationToken);
        if (current.Result is not null)
        {
            return current.Result;
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest("CurrentPassword and NewPassword are required.");
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        var user = current.User!;
        var changeResult = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!changeResult.Succeeded)
        {
            await auditService.WriteAsync(
                new AuditWriteRequest(
                    tenantId,
                    "portal.account.password.change.failed",
                    $"User {user.UserName} password change failed.",
                    "failed",
                    "user",
                    user.Id,
                    RequestId: HttpContext.TraceIdentifier,
                    SessionId: ResolveSessionId(),
                    UserAgent: Request.Headers.UserAgent.ToString(),
                    IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString()),
                cancellationToken);
            return BadRequest(changeResult.Errors.Select(x => x.Description));
        }

        user.UpdateTime = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);
        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "portal.account.password.changed",
                $"User {user.UserName} changed password.",
                ActorType: "user",
                ActorId: user.Id,
                RequestId: HttpContext.TraceIdentifier,
                SessionId: ResolveSessionId(),
                UserAgent: Request.Headers.UserAgent.ToString(),
                IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return Ok(new { changed = true });
    }

    [HttpGet("apps")]
    public async Task<IActionResult> ListApplications(CancellationToken cancellationToken)
    {
        var current = await ResolveCurrentUserAsync(cancellationToken);
        if (current.Result is not null)
        {
            return current.Result;
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        var user = current.User!;
        var groupIds = await dbContext.UserGroupMembers
            .Where(x => x.TenantId == tenantId && x.UserId == user.Id && !x.IsDeleted)
            .Select(x => x.UserGroupId)
            .ToListAsync(cancellationToken);
        var organizationIds = await dbContext.OrganizationMembers
            .Where(x => x.TenantId == tenantId && x.UserId == user.Id && !x.IsDeleted)
            .Select(x => x.OrganizationId)
            .ToListAsync(cancellationToken);

        var policies = await dbContext.AppAccessPolicies
            .Where(x => x.TenantId == tenantId
                        && !x.IsDeleted
                        && (x.SubjectType == SubjectType.User && x.SubjectId == user.Id
                            || x.SubjectType == SubjectType.Group && groupIds.Contains(x.SubjectId)
                            || x.SubjectType == SubjectType.Organization && organizationIds.Contains(x.SubjectId)))
            .ToListAsync(cancellationToken);

        var allowedAppIds = policies
            .GroupBy(x => x.AppId, StringComparer.Ordinal)
            .Where(x => x.Any(policy => policy.AllowAccess) && !x.Any(policy => !policy.AllowAccess))
            .Select(x => x.Key)
            .ToList();

        if (allowedAppIds.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var apps = await dbContext.Apps
            .Where(x => x.TenantId == tenantId && allowedAppIds.Contains(x.Id) && x.Enabled && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Code)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.Protocol,
                x.Enabled
            })
            .ToListAsync(cancellationToken);

        return Ok(apps);
    }

    [HttpGet("bindings")]
    public async Task<IActionResult> ListBindings(CancellationToken cancellationToken)
    {
        var current = await ResolveCurrentUserAsync(cancellationToken);
        if (current.Result is not null)
        {
            return current.Result;
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        var user = current.User!;
        var bindings = await dbContext.UserIdpBinds
            .Where(x => x.TenantId == tenantId && x.UserId == user.Id && !x.IsDeleted)
            .Join(
                dbContext.ThirdPartyUsers.Where(x => x.TenantId == tenantId && !x.IsDeleted),
                binding => binding.ThirdPartyUserId,
                thirdParty => thirdParty.Id,
                (binding, thirdParty) => new { binding, thirdParty })
            .GroupJoin(
                dbContext.IdentityProviders.Where(x => x.TenantId == tenantId && !x.IsDeleted),
                joined => joined.thirdParty.IdentityProviderId,
                provider => provider.Id,
                (joined, providers) => new { joined.binding, joined.thirdParty, provider = providers.FirstOrDefault() })
            .OrderByDescending(x => x.binding.BoundTime)
            .Select(x => new
            {
                id = x.binding.Id,
                x.binding.BoundTime,
                providerId = x.provider == null ? x.thirdParty.IdentityProviderId : x.provider.Id,
                providerCode = x.provider == null ? null : x.provider.Code,
                providerName = x.provider == null ? null : x.provider.Name,
                providerType = x.provider == null ? null : (int?)x.provider.ProviderType,
                x.thirdParty.OpenId,
                x.thirdParty.UnionId,
                name = x.thirdParty.Name,
                email = x.thirdParty.Email,
                mobile = x.thirdParty.Mobile,
                avatarUrl = x.thirdParty.AvatarUrl,
                x.thirdParty.LastLoginTime
            })
            .ToListAsync(cancellationToken);

        return Ok(bindings);
    }

    [HttpDelete("bindings/{bindingId}")]
    public async Task<IActionResult> Unbind(string bindingId, CancellationToken cancellationToken)
    {
        var current = await ResolveCurrentUserAsync(cancellationToken);
        if (current.Result is not null)
        {
            return current.Result;
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        var user = current.User!;
        var binding = await dbContext.UserIdpBinds
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == bindingId && x.UserId == user.Id && !x.IsDeleted, cancellationToken);
        if (binding is null)
        {
            return NotFound();
        }

        binding.IsDeleted = true;
        binding.UpdateTime = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "portal.account.idp.unbound",
                $"User {user.UserName} unbound third-party account.",
                ActorType: "user",
                ActorId: user.Id,
                TargetJson: $$"""{"bindingId":"{{binding.Id}}","thirdPartyUserId":"{{binding.ThirdPartyUserId}}"}""",
                RequestId: HttpContext.TraceIdentifier,
                SessionId: ResolveSessionId(),
                UserAgent: Request.Headers.UserAgent.ToString(),
                IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return NoContent();
    }

    [HttpGet("audit")]
    public async Task<IActionResult> ListAudit([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var current = await ResolveCurrentUserAsync(cancellationToken);
        if (current.Result is not null)
        {
            return current.Result;
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        var user = current.User!;
        var normalizedTake = Math.Clamp(take, 1, 200);
        var entries = await dbContext.AuditEvents
            .Where(x => x.TenantId == tenantId && x.ActorId == user.Id && !x.IsDeleted)
            .OrderByDescending(x => x.OccurredTime)
            .Take(normalizedTake)
            .Select(x => new
            {
                x.Id,
                x.EventType,
                x.Content,
                x.ResultStatus,
                x.RequestId,
                x.SessionId,
                x.UserAgent,
                x.IpAddress,
                x.GeoLocation,
                x.OccurredTime
            })
            .ToListAsync(cancellationToken);

        return Ok(entries);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> ListSessions([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var current = await ResolveCurrentUserAsync(cancellationToken);
        if (current.Result is not null)
        {
            return current.Result;
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        var user = current.User!;
        var normalizedTake = Math.Clamp(take, 1, 200);
        var revokedSet = (await sessionRevocationService.GetRevokedSessionIdsAsync(tenantId, cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var entries = await dbContext.AuditEvents
            .Where(x => x.TenantId == tenantId
                        && x.ActorId == user.Id
                        && !x.IsDeleted
                        && (x.EventType == "auth.local-login.succeeded"
                            || x.EventType == "portal.idp.login.callback"))
            .OrderByDescending(x => x.OccurredTime)
            .Take(normalizedTake)
            .ToListAsync(cancellationToken);

        return Ok(entries.Select(x =>
        {
            var sessionId = ResolveSessionId(x);
            return new
            {
                sessionId,
                x.EventType,
                x.ResultStatus,
                x.IpAddress,
                x.UserAgent,
                x.OccurredTime,
                revoked = revokedSet.Contains(sessionId)
            };
        }));
    }

    [HttpPost("sessions/{sessionId}/revoke")]
    public async Task<IActionResult> RevokeSession(string sessionId, CancellationToken cancellationToken)
    {
        var current = await ResolveCurrentUserAsync(cancellationToken);
        if (current.Result is not null)
        {
            return current.Result;
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest("SessionId is required.");
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        var user = current.User!;
        var normalizedSessionId = sessionId.Trim();
        var sessionExists = await dbContext.AuditEvents.AnyAsync(
            x => x.TenantId == tenantId
                 && x.ActorId == user.Id
                 && !x.IsDeleted
                 && (x.SessionId == normalizedSessionId || x.RequestId == normalizedSessionId || x.Id == normalizedSessionId),
            cancellationToken);
        if (!sessionExists)
        {
            return NotFound("Session not found for current user.");
        }

        await sessionRevocationService.RevokeAsync(tenantId, normalizedSessionId, cancellationToken);
        var terminationResult = await sessionTerminationService.TerminateBySessionIdAsync(tenantId, normalizedSessionId, cancellationToken);
        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "portal.session.revoked",
                $"User {user.UserName} revoked session {normalizedSessionId}.",
                ActorType: "user",
                ActorId: user.Id,
                TargetJson: $$"""{"sessionId":"{{normalizedSessionId}}","revokedTokens":{{terminationResult.RevokedTokens}},"revokedAuthorizations":{{terminationResult.RevokedAuthorizations}}}""",
                RequestId: HttpContext.TraceIdentifier,
                SessionId: ResolveSessionId(),
                UserAgent: Request.Headers.UserAgent.ToString(),
                IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return Ok(new
        {
            sessionId = normalizedSessionId,
            revoked = true,
            terminationResult.RevokedTokens,
            terminationResult.RevokedAuthorizations,
            terminationResult.AffectedUsers
        });
    }

    private async Task<(NetIamIdentityUser? User, IActionResult? Result)> ResolveCurrentUserAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub")
                     ?? Request.Headers[RbacAuthorizationDefaults.ActingUserHeader].ToString();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (null, Unauthorized("Missing current user. Supply a bearer token or X-Acting-User-Id."));
        }

        var user = await userManager.Users.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Id == userId && !x.IsDeleted,
            cancellationToken);
        return user is null
            ? (null, NotFound("Current user not found."))
            : (user, null);
    }

    private string ResolveSessionId()
    {
        var claimSid = User.FindFirstValue("sid");
        if (!string.IsNullOrWhiteSpace(claimSid))
        {
            return claimSid;
        }

        return Request.Headers[SessionRevocationDefaults.SessionIdHeader].ToString();
    }

    private static string ResolveSessionId(AuditEventEntity entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.SessionId))
        {
            return entry.SessionId;
        }

        if (!string.IsNullOrWhiteSpace(entry.RequestId))
        {
            return entry.RequestId;
        }

        return entry.Id;
    }

    private static object BuildUserProfile(NetIamIdentityUser user)
    {
        return new
        {
            user.Id,
            userName = user.UserName,
            user.DisplayName,
            user.Email,
            user.PhoneNumber,
            user.ExternalId,
            user.EmailConfirmed,
            user.PhoneNumberConfirmed,
            user.TwoFactorEnabled,
            user.LockoutEnd,
            user.AccessFailedCount,
            user.CreateTime,
            user.UpdateTime
        };
    }
}