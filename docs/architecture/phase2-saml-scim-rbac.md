# Phase 2: SAML / SCIM / RBAC

## Capability Summary

Phase 2 extends NetIAM with:

- fine-grained RBAC using permission catalog + role permission + user grant overlay
- SAML IdP endpoints for metadata and HTTP-POST SSO
- SCIM 2.0 provisioning endpoints for Users and Groups with bearer token validation
- real external directory API mode for DingTalk, Feishu, and WeCom

## RBAC Design

### Data Model

- `eiam_permission`
- `eiam_role_permission`
- `eiam_user_permission_grant`

### Resolution Order

1. user-level explicit grant (`Allow` or `Deny`)
2. role-level permission grants
3. deny has precedence over role allow

### Runtime Enforcement

- policy prefix: `perm:{permissionCode}`
- attribute: `RequirePermissionAttribute`
- policy provider: `RbacPermissionPolicyProvider`
- authorization handler: `RbacPermissionAuthorizationHandler`
- user identity source:
  - bearer claims `sub` / `nameidentifier`
  - fallback header `X-Acting-User-Id` for local/dev calls

## SAML Design

### Core Tables

- `eiam_saml_service_provider`

### AuthServer Endpoints

- `GET /saml2/metadata/{tenantId?}` returns IdP metadata
- `GET /saml2/sso/{serviceProviderCode}` generates SAMLResponse and auto-post html
- `POST /saml2/acs/{serviceProviderCode}` receives and previews response payload

### Current Characteristics

- SAMLResponse is generated as unsigned assertion for integration bootstrap
- supports SP-specific ACS, Audience, NameID format, relay state defaults

## SCIM 2.0 Design

### Core Tables

- `eiam_scim_access_token`

### Token Management

- admin endpoint: `POST /api/admin/scim/tokens`
- stored as SHA256 hash, plain token returned only on create
- token validation done by `ScimTokenAuthorizeAttribute`

### SCIM Endpoints

- `GET/POST/PUT/PATCH/DELETE /scim/v2/Users`
- `GET/POST/PUT/PATCH/DELETE /scim/v2/Groups`

Mapping:

- SCIM User -> `NetIamIdentityUser`
- SCIM Group -> `UserGroupEntity`
- SCIM Group members -> `UserGroupMemberEntity`

## Real Provider API Mode

`IdentitySourceEntity.BasicConfigJson` supports:

- `useMock: true|false` (default false)
- provider credentials:
  - DingTalk: `appKey`, `appSecret`
  - Feishu: `appId`, `appSecret`
  - WeCom: `corpId`, `appSecret`

When credentials are present and `useMock=false`, directory sync providers call real external APIs.
