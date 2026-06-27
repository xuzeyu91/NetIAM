using Microsoft.EntityFrameworkCore;
using NetIAM.Infrastructure.Persistence;
using OpenIddict.Abstractions;

namespace NetIAM.Infrastructure.Services;

public interface ISessionRevocationService
{
    Task<IReadOnlyCollection<string>> GetRevokedSessionIdsAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<bool> IsRevokedAsync(string tenantId, string sessionId, CancellationToken cancellationToken = default);

    Task RevokeAsync(string tenantId, string sessionId, CancellationToken cancellationToken = default);
}

public interface ISessionTerminationService
{
    Task<SessionTerminationResult> TerminateBySessionIdAsync(
        string tenantId,
        string sessionId,
        CancellationToken cancellationToken = default);
}

public sealed record SessionTerminationResult(int RevokedTokens, int RevokedAuthorizations, int AffectedUsers);

internal sealed record RevokedSessionRegistry(IReadOnlyCollection<string> SessionIds);

public sealed class SessionRevocationService(ISystemSettingStore systemSettingStore) : ISessionRevocationService
{
    public const string RevokedSessionSettingKey = "monitor.revoked-sessions";

    public async Task<IReadOnlyCollection<string>> GetRevokedSessionIdsAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var registry = await systemSettingStore.GetAsync(
            tenantId,
            RevokedSessionSettingKey,
            new RevokedSessionRegistry(Array.Empty<string>()),
            cancellationToken);
        return registry.SessionIds;
    }

    public async Task<bool> IsRevokedAsync(string tenantId, string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        var revoked = await GetRevokedSessionIdsAsync(tenantId, cancellationToken);
        return revoked.Contains(sessionId.Trim(), StringComparer.Ordinal);
    }

    public async Task RevokeAsync(string tenantId, string sessionId, CancellationToken cancellationToken = default)
    {
        var normalizedSessionId = sessionId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            return;
        }

        var revoked = await GetRevokedSessionIdsAsync(tenantId, cancellationToken);
        var revokedSet = revoked.ToHashSet(StringComparer.Ordinal);
        revokedSet.Add(normalizedSessionId);

        var updatedRegistry = new RevokedSessionRegistry(revokedSet.OrderBy(x => x, StringComparer.Ordinal).ToArray());
        await systemSettingStore.SetAsync(tenantId, RevokedSessionSettingKey, updatedRegistry, cancellationToken);
    }
}

public sealed class SessionTerminationService(
    NetIamDbContext dbContext,
    IOpenIddictTokenManager tokenManager,
    IOpenIddictAuthorizationManager authorizationManager) : ISessionTerminationService
{
    public async Task<SessionTerminationResult> TerminateBySessionIdAsync(
        string tenantId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var normalizedSessionId = sessionId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            return new SessionTerminationResult(0, 0, 0);
        }

        var affectedUserIds = await dbContext.AuditEvents
            .Where(x => x.TenantId == tenantId
                        && (x.SessionId == normalizedSessionId
                            || x.RequestId == normalizedSessionId
                            || x.Id == normalizedSessionId)
                        && !string.IsNullOrWhiteSpace(x.ActorId))
            .Select(x => x.ActorId!)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (affectedUserIds.Count == 0)
        {
            return new SessionTerminationResult(0, 0, 0);
        }

        var revokedTokens = 0;
        var revokedAuthorizations = 0;
        foreach (var userId in affectedUserIds)
        {
            await foreach (var token in tokenManager.FindBySubjectAsync(userId, cancellationToken))
            {
                if (await tokenManager.TryRevokeAsync(token, cancellationToken))
                {
                    revokedTokens++;
                }
            }

            await foreach (var authorization in authorizationManager.FindBySubjectAsync(userId, cancellationToken))
            {
                if (await authorizationManager.TryRevokeAsync(authorization, cancellationToken))
                {
                    revokedAuthorizations++;
                }
            }
        }

        return new SessionTerminationResult(revokedTokens, revokedAuthorizations, affectedUserIds.Count);
    }
}
