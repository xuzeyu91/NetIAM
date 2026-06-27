namespace NetIAM.Infrastructure.Services;

public interface ITenantContextAccessor
{
    string GetTenantId();
}

public sealed record AuditWriteRequest(
    string TenantId,
    string EventType,
    string Content,
    string ResultStatus = "success",
    string ActorType = "system",
    string? ActorId = null,
    string? TargetJson = null,
    string? RequestId = null,
    string? SessionId = null,
    string? UserAgent = null,
    string? IpAddress = null);

public interface IAuditService
{
    Task WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default);
}
