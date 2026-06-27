# NetIAM Architecture

## High-level Topology

```mermaid
flowchart LR
  adminUi[AdminReact] --> adminApi[NetIAM.AdminApi]
  portalUi[PortalReact] --> portalApi[NetIAM.PortalApi]
  enterpriseApp[EnterpriseApps] --> authServer[NetIAM.AuthServer]

  adminApi --> db[(PostgreSQL)]
  portalApi --> db
  authServer --> db
  syncWorker[NetIAM.SyncWorker] --> db

  adminApi --> redis[(Redis)]
  portalApi --> redis
  authServer --> redis

  portalApi --> dingTalk[DingTalkOAuth]
  portalApi --> feishu[FeishuOAuth]
  portalApi --> wecom[WeComOAuth]
  syncWorker --> dingTalk
  syncWorker --> feishu
  syncWorker --> wecom
```

## Core Services

- `NetIAM.AuthServer`
  - OpenIddict server endpoints (`/connect/authorize`, `/connect/token`, `/connect/introspect`, `/connect/userinfo`)
  - local account login endpoint (`/api/auth/local-login`)
  - OIDC client bootstrap endpoint (`/api/bootstrap/oidc/clients`)
  - SAML endpoints (`/saml2/metadata`, `/saml2/sso`, `/saml2/acs`)
- `NetIAM.AdminApi`
  - user/organization/tenant/provider/source/app/audit admin APIs
  - identity source sync trigger and webhook receive endpoint
  - RBAC APIs (`/api/admin/rbac/*`)
  - SAML SP management APIs (`/api/admin/saml/service-providers`)
  - SCIM token management (`/api/admin/scim/tokens`)
  - SCIM 2.0 provisioning (`/scim/v2/Users`, `/scim/v2/Groups`)
- `NetIAM.PortalApi`
  - SSO authorization entry (`/authn/{provider}/{code}`)
  - callback processing (`/login/{provider}/{code}`)
  - existing account binding (`/login/bind`)
- `NetIAM.SyncWorker`
  - periodic pull sync from configured identity sources
  - sync history and records persistence

## Domain Mapping (from eiam)

- `IdentityProviderEntity` and `IdentitySourceEntity` are separated
- `ThirdPartyUserEntity` + `UserIdpBindEntity` keeps external login mapping
- `NetIamIdentityUser.ExternalId` enables auto-bind strategy
- `IdentitySourceSyncHistoryEntity` + `IdentitySourceSyncRecordEntity` stores synchronization audit trail
- `AuditEventEntity` records key authentication/admin/sync events
- `PermissionEntity` + `RolePermissionEntity` + `UserPermissionGrantEntity` enable fine-grained RBAC
- `SamlServiceProviderEntity` stores per-tenant SAML SP integration config
- `ScimAccessTokenEntity` stores hashed SCIM API bearer tokens

## Security & Observability

- password policy and lockout configured by ASP.NET Core Identity
- data protection keys persisted in Redis for multi-node consistency
- request limiting added by fixed-window rate limiter
- Serilog structured logs enabled on all backend services
