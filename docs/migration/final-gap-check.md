# NetIAM Final Capability Check (vs eiam Core)

## Completed Core Capabilities

- local identity
  - tenant/user/organization CRUD APIs
  - password policy, lockout, seeded admin account
  - audit event persistence
- SSO provider integration
  - DingTalk / Feishu / WeCom adapter handlers
  - authorize URL build + callback token/profile processing
- account binding
  - `ThirdPartyUser` + `UserIdpBind` tables and service
  - auto-bind fallback by `ExternalId == openId`
  - pending-binding and explicit bind API
- identity source sync
  - pull sync orchestration
  - webhook normalization endpoint
  - sync history and sync record persistence
- OIDC/JWT base
  - OpenIddict server wired with EF stores
  - OIDC client bootstrap endpoint
- phase2 extension
  - fine-grained RBAC (`permission` / `role_permission` / `user_permission_grant`)
  - SCIM 2.0 (`/scim/v2/Users`, `/scim/v2/Groups`) with token-based authorization
  - SAML IdP (`/saml2/metadata`, `/saml2/sso`, `/saml2/acs`)
  - real provider API mode for DingTalk/Feishu/WeCom directory sync
- operational baseline
  - Redis-backed data protection
  - fixed-window request limiting
  - Serilog + OpenTelemetry tracing
  - docker-compose for PostgreSQL/Redis

## Verified in This Migration Session

- `dotnet build src/NetIAM.sln` passes
- `npm run build` passes for `web/admin` and `web/portal`
- admin closure milestone delivered:
  - `web/admin` now covers tenant/user/organization/app/provider/source/rbac/saml/scim/audit tabs
  - request context supports `X-Tenant-Id` + `X-Acting-User-Id` + optional bearer token
  - users upgraded from read-only to full CRUD UX
- admin backend minimal CRUD gaps closed:
  - tenants support update/delete
  - organizations support update/delete with tree-path rebuild and child-delete guard
  - apps support update/delete
  - rbac roles support create/delete
- capability baseline document added:
  - `docs/migration/admin-capability-gap.md`
- EF migration generated successfully:
  - `src/NetIAM.Infrastructure/Persistence/Migrations/20260627053351_InitialCoreSchema.cs`
  - `src/NetIAM.Infrastructure/Persistence/Migrations/20260627055838_Phase2SamlScimRbac.cs`

## Remaining Gaps for Next Iteration

- admin modules not yet aligned to eiam console breadth:
  - user-group management and app-access-policy management
  - security settings (password policy / defense policy / administrator management)
  - monitor/session management and system setting modules (message/geoip/storage)
- identity source sync history/record query APIs and admin UI
- SAML assertion signing/encryption and metadata certificate rollover automation
- SCIM PATCH filter semantics and bulk endpoint support
- provider sandbox contract tests + retry/backoff + rate-limit governance
- full end-to-end automated integration test suite with real provider sandbox credentials
