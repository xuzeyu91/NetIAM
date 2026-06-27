using NetIAM.Domain.Abstractions;
using NetIAM.Domain.Enums;

namespace NetIAM.Domain.Entities;

public sealed class TenantEntity : AuditedEntityBase
{
    public string Identifier { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public string? DefaultDomain { get; set; }
}

public sealed class UserEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public string? ExternalId { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public bool EmailConfirmed { get; set; }

    public bool PhoneNumberConfirmed { get; set; }

    public bool LockoutEnabled { get; set; } = true;

    public DateTimeOffset? LockoutEnd { get; set; }

    public int AccessFailedCount { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public DataOriginType DataOrigin { get; set; } = DataOriginType.Local;
}

public sealed class OrganizationEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? ParentId { get; set; }

    public string Path { get; set; } = "/";

    public string DisplayPath { get; set; } = "/";

    public string? ExternalId { get; set; }

    public string? IdentitySourceId { get; set; }

    public DataOriginType DataOrigin { get; set; } = DataOriginType.Local;
}

public sealed class OrganizationMemberEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string OrganizationId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;
}

public sealed class UserGroupEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public sealed class UserGroupMemberEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string UserGroupId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;
}

public sealed class IdentityProviderEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public ExternalProviderType ProviderType { get; set; }

    public bool Enabled { get; set; } = true;

    public string ConfigJson { get; set; } = "{}";
}

public sealed class IdentitySourceEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public IdentitySourceProviderType ProviderType { get; set; }

    public bool Enabled { get; set; } = true;

    public string BasicConfigJson { get; set; } = "{}";

    public string StrategyConfigJson { get; set; } = "{}";

    public string JobConfigJson { get; set; } = "{}";
}

public sealed class ThirdPartyUserEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string IdentityProviderId { get; set; } = string.Empty;

    public string OpenId { get; set; } = string.Empty;

    public string? UnionId { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Mobile { get; set; }

    public string? AvatarUrl { get; set; }

    public string RawProfileJson { get; set; } = "{}";

    public DateTimeOffset LastLoginTime { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UserIdpBindEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string ThirdPartyUserId { get; set; } = string.Empty;

    public DateTimeOffset BoundTime { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AppEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Protocol { get; set; } = "oidc";

    public bool Enabled { get; set; } = true;
}

public sealed class AppAccessPolicyEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppId { get; set; } = string.Empty;

    public SubjectType SubjectType { get; set; }

    public string SubjectId { get; set; } = string.Empty;

    public bool AllowAccess { get; set; } = true;
}

public sealed class PermissionEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Resource { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public sealed class RolePermissionEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string RoleId { get; set; } = string.Empty;

    public string PermissionId { get; set; } = string.Empty;
}

public sealed class UserPermissionGrantEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string PermissionId { get; set; } = string.Empty;

    public PermissionGrantEffect Effect { get; set; } = PermissionGrantEffect.Allow;
}

public sealed class SamlServiceProviderEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string AssertionConsumerServiceUrl { get; set; } = string.Empty;

    public string? SingleLogoutServiceUrl { get; set; }

    public string NameIdFormat { get; set; } = "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified";

    public string? Audience { get; set; }

    public string? RelayStateDefault { get; set; }

    public bool WantSignedAssertions { get; set; } = true;

    public bool AllowUnsolicitedResponse { get; set; }

    public SamlBindingType BindingType { get; set; } = SamlBindingType.HttpPost;

    public string? SigningCertificatePem { get; set; }

    public bool Enabled { get; set; } = true;
}

public sealed class ScimAccessTokenEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset? ExpiresTime { get; set; }

    public DateTimeOffset? LastUsedTime { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class SystemSettingEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string SettingKey { get; set; } = string.Empty;

    public string ValueJson { get; set; } = "{}";
}

public sealed class IdentitySourceSyncHistoryEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string IdentitySourceId { get; set; } = string.Empty;

    public string TriggerMode { get; set; } = "pull";

    public SyncStatus Status { get; set; } = SyncStatus.Success;

    public int TotalUsers { get; set; }

    public int CreatedUsers { get; set; }

    public int UpdatedUsers { get; set; }

    public int DeletedUsers { get; set; }

    public int SkippedUsers { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset StartedTime { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndedTime { get; set; }
}

public sealed class IdentitySourceSyncRecordEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string SyncHistoryId { get; set; } = string.Empty;

    public string ObjectType { get; set; } = string.Empty;

    public string ObjectId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;

    public string? Detail { get; set; }
}

public sealed class AuditEventEntity : AuditedEntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string? ActorId { get; set; }

    public string ActorType { get; set; } = "system";

    public string EventType { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? RequestId { get; set; }

    public string? SessionId { get; set; }

    public string? TargetJson { get; set; }

    public string? UserAgent { get; set; }

    public string? IpAddress { get; set; }

    public string? GeoLocation { get; set; }

    public string ResultStatus { get; set; } = "success";

    public DateTimeOffset OccurredTime { get; set; } = DateTimeOffset.UtcNow;
}
