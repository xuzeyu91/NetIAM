using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/monitor/sessions")]
public sealed class SessionMonitorController(
    NetIamDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ISystemSettingStore systemSettingStore,
    IAuditService auditService) : ControllerBase
{
    private const string RevokedSessionSettingKey = "monitor.revoked-sessions";

    public sealed record RevokedSessionRegistry(IReadOnlyCollection<string> SessionIds);

    [HttpGet]
    [RequirePermission("monitor.read")]
    public async Task<IActionResult> List([FromQuery] int take = 100, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var normalizedTake = Math.Clamp(take, 1, 500);
        var revoked = await systemSettingStore.GetAsync(
            tenantId,
            RevokedSessionSettingKey,
            new RevokedSessionRegistry(Array.Empty<string>()),
            cancellationToken);
        var revokedSet = revoked.SessionIds.ToHashSet(StringComparer.Ordinal);

        var entries = await dbContext.AuditEvents
            .Where(x => x.TenantId == tenantId
                        && (x.EventType == "auth.local-login.succeeded"
                            || x.EventType == "portal.idp.login.callback"))
            .OrderByDescending(x => x.OccurredTime)
            .Take(normalizedTake)
            .ToListAsync(cancellationToken);

        var userIds = entries
            .Select(x => x.ActorId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var users = userIds.Count == 0
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await dbContext.Users
                .Where(x => x.TenantId == tenantId && userIds.Contains(x.Id) && !x.IsDeleted)
                .ToDictionaryAsync(
                    x => x.Id,
                    x => string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName,
                    cancellationToken);

        var sessions = entries.Select(x =>
        {
            var sessionId = ResolveSessionId(x);
            return new
            {
                sessionId,
                userId = x.ActorId,
                userName = string.IsNullOrWhiteSpace(x.ActorId) ? null : users.GetValueOrDefault(x.ActorId),
                x.EventType,
                x.ResultStatus,
                x.IpAddress,
                x.UserAgent,
                x.OccurredTime,
                revoked = revokedSet.Contains(sessionId)
            };
        });

        return Ok(sessions);
    }

    [HttpPost("{sessionId}/revoke")]
    [RequirePermission("monitor.write")]
    public async Task<IActionResult> Revoke(string sessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return BadRequest("SessionId is required.");
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        var registry = await systemSettingStore.GetAsync(
            tenantId,
            RevokedSessionSettingKey,
            new RevokedSessionRegistry(Array.Empty<string>()),
            cancellationToken);

        var revokedSet = registry.SessionIds.ToHashSet(StringComparer.Ordinal);
        revokedSet.Add(sessionId.Trim());
        var updatedRegistry = new RevokedSessionRegistry(revokedSet.OrderBy(x => x, StringComparer.Ordinal).ToArray());

        await systemSettingStore.SetAsync(tenantId, RevokedSessionSettingKey, updatedRegistry, cancellationToken);
        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.monitor.session.revoked",
                $"Session {sessionId} marked as revoked.",
                TargetJson: $$"""{"sessionId":"{{sessionId}}"}"""),
            cancellationToken);

        return Ok(new { sessionId, revoked = true });
    }

    private static string ResolveSessionId(NetIAM.Domain.Entities.AuditEventEntity entry)
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
}
