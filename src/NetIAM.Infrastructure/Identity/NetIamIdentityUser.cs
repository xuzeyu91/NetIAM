using Microsoft.AspNetCore.Identity;
using NetIAM.Domain.Enums;

namespace NetIAM.Infrastructure.Identity;

public sealed class NetIamIdentityUser : IdentityUser
{
    public string TenantId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? ExternalId { get; set; }

    public DataOriginType DataOrigin { get; set; } = DataOriginType.Local;

    public string? CreateBy { get; set; }

    public DateTimeOffset CreateTime { get; set; } = DateTimeOffset.UtcNow;

    public string? UpdateBy { get; set; }

    public DateTimeOffset UpdateTime { get; set; } = DateTimeOffset.UtcNow;

    public bool IsDeleted { get; set; }

    public string? Remark { get; set; }
}
