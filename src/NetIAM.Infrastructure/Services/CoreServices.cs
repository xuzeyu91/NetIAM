using Microsoft.AspNetCore.Http;
using NetIAM.Domain.Entities;
using NetIAM.Infrastructure.Persistence;

namespace NetIAM.Infrastructure.Services;

public sealed class HttpHeaderTenantContextAccessor(IHttpContextAccessor httpContextAccessor) : ITenantContextAccessor
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string FallbackTenant = "default";

    public string GetTenantId()
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null)
        {
            return FallbackTenant;
        }

        return context.Request.Headers.TryGetValue(TenantHeader, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : FallbackTenant;
    }
}

public sealed class AuditService(NetIamDbContext dbContext) : IAuditService
{
    public async Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default)
    {
        var entry = new AuditEventEntity
        {
            TenantId = request.TenantId,
            EventType = request.EventType,
            Content = request.Content,
            ResultStatus = request.ResultStatus,
            ActorType = request.ActorType,
            ActorId = request.ActorId,
            TargetJson = request.TargetJson,
            RequestId = request.RequestId,
            SessionId = request.SessionId,
            UserAgent = request.UserAgent,
            IpAddress = request.IpAddress,
            OccurredTime = DateTimeOffset.UtcNow
        };

        dbContext.AuditEvents.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
