namespace NetIAM.Domain.Contracts;

public sealed record ExternalAuthRequest(
    string TenantId,
    string ProviderCode,
    string CallbackBaseUri,
    string State,
    string? LoginHint = null);

public sealed record ExternalAuthCallback(
    string TenantId,
    string ProviderCode,
    string AuthorizationCode,
    string State);

public sealed record ExternalUserProfile(
    string OpenId,
    string? UnionId,
    string? Name,
    string? Email,
    string? Mobile,
    string? AvatarUrl,
    string RawProfileJson);

public sealed record ExternalUserAccessToken(
    string AccessToken,
    DateTimeOffset ExpiresAt);

public sealed record DirectoryUserSnapshot(
    string ExternalId,
    string Username,
    string DisplayName,
    string? Email,
    string? Mobile,
    string? DepartmentExternalId);

public sealed record DirectoryOrganizationSnapshot(
    string ExternalId,
    string Name,
    string? ParentExternalId);

public sealed record DirectoryNormalizedEvent(
    string EventType,
    string ExternalId,
    string PayloadJson);

public sealed record SamlSsoRequest(
    string TenantId,
    string ServiceProviderCode,
    string UserId,
    string? RelayState);

public sealed record SamlSsoResponse(
    string Issuer,
    string Destination,
    string SubjectNameId,
    string Audience,
    string EncodedSamlResponse);

public sealed record ScimPrincipalContext(
    string TenantId,
    string TokenName,
    string? RequestId = null);
