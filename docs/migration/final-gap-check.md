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
  - `web/admin` now covers tenant/user/user-group/organization/app/access-policy/security/settings/monitor/provider/source/rbac/saml/scim/audit tabs
  - request context supports `X-Tenant-Id` + `X-Acting-User-Id` + optional bearer token
  - users upgraded from read-only to full CRUD UX
- phase2 admin modules delivered:
  - admin user-group APIs (`/api/admin/user-groups`) with member replacement
  - admin app-access-policy APIs (`/api/admin/app-access-policies`) with subject validation
  - admin UI wiring for user-group and app-access-policy management
- phase3 admin modules delivered:
  - security settings APIs (`/api/admin/security/basic|password-policy|defense-policy`)
  - administrator management APIs (`/api/admin/security/administrators`)
  - system settings APIs (`/api/admin/settings/message|storage|geoip`)
  - monitor session APIs (`/api/admin/monitor/sessions`) with revocation marker support
  - tenant-scoped system setting persistence table `eiam_system_setting`
- phase4 depth modules delivered:
  - identity source sync history/record query APIs (`/api/admin/identity-sources/{code}/sync-histories|sync-records`)
  - admin source tab now visualizes sync histories and record details with history filtering
  - RBAC user-role binding APIs (`/api/admin/rbac/users/{userId}/roles`) and UI operations
  - RBAC user grant revoke APIs (`DELETE /api/admin/rbac/users/{userId}/grants/{permissionCode}`) and UI operations
- phase5 hardening modules delivered:
  - monitor hard-logout guard (`X-Session-Id`/`sid`) + session revocation middleware in admin/portal/auth services
  - session termination service wired to OpenIddict token/authorization revoke path
  - auth callback/local login now emit stable `sessionId`; admin request context now supports `X-Session-Id`
  - SAML assertion signing + optional encryption (`SigningCertificatePem`) + metadata signing key publication
  - SAML IdP signing certificate auto-generation and rollover automation via system settings
  - SCIM users/groups PATCH semantics enhanced (`add`/`replace`/`remove`, filtered member removal) and `/scim/v2/Bulk` endpoint added
  - provider outbound governance: retry/backoff (429/5xx), retry-after handling, and base rate throttling
  - contract/e2e test suites added (`tests/NetIAM.Integrations.ContractTests`, `tests/NetIAM.E2E.Tests`) with env-gated sandbox credentials
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
  - `src/NetIAM.Infrastructure/Persistence/Migrations/20260627103708_Phase3SecuritySettingsMonitor.cs`

## Remaining Gaps for Next Iteration

- OIDC/SAML end-session protocol completeness (front-channel logout handshake and full SLO interop).
- RBAC permission bulk policy templates/operations.
- CI pipeline integration for sandbox contract/e2e suites with managed secret rotation.
