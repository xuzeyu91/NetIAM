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
- operational baseline
  - Redis-backed data protection
  - fixed-window request limiting
  - Serilog + OpenTelemetry tracing
  - docker-compose for PostgreSQL/Redis

## Verified in This Migration Session

- `dotnet build src/NetIAM.sln` passes
- `npm run build` passes for `web/admin` and `web/portal`
- EF migration generated successfully:
  - `src/NetIAM.Infrastructure/Persistence/Migrations/20260627053351_InitialCoreSchema.cs`

## Remaining Gaps for Next Iteration

- full enterprise protocol parity (SAML/SCIM not yet included)
- production-grade provider APIs (currently adapter scaffolding + config-driven pull stubs for directory sync)
- admin RBAC fine-grained permissions
- full end-to-end automated integration test suite with real provider sandbox credentials
