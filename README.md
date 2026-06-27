# NetIAM

NetIAM is a .NET 10 based IAM platform that references eiam architecture patterns and implements:

- local identity management (tenant, user, organization, audit)
- enterprise SSO adapters for DingTalk, Feishu, and WeCom
- account binding (`ThirdPartyUser` + `UserIdpBind`) with auto-link by `ExternalId`
- directory sync engine (pull + webhook normalization)
- OpenIddict-based OIDC/JWT capability for downstream applications

## Repository Structure

- `src/NetIAM.AuthServer`: authentication and OpenIddict server
- `src/NetIAM.AdminApi`: IAM admin APIs (users, providers, sources, apps, audit)
- `src/NetIAM.PortalApi`: portal SSO entry/callback and bind workflow
- `src/NetIAM.SyncWorker`: background identity-source synchronization worker
- `src/NetIAM.Domain`: domain entities and contracts
- `src/NetIAM.Infrastructure`: EF Core, Identity, migration and core services
- `src/NetIAM.Integrations`: DingTalk/Feishu/WeCom provider adapters
- `web/admin`: React admin console
- `web/portal`: React portal app

## Quick Start

1. Start infrastructure dependencies:

   ```bash
   docker compose -f deploy/docker-compose.yml up -d
   ```

2. Build backend:

   ```bash
   dotnet build src/NetIAM.sln
   ```

3. Apply migration (from workspace root):

   ```bash
   dotnet ef database update --project src/NetIAM.Infrastructure/NetIAM.Infrastructure.csproj
   ```

4. Run backend services (separate terminals):

   ```bash
   dotnet run --project src/NetIAM.AuthServer/NetIAM.AuthServer.csproj
   dotnet run --project src/NetIAM.AdminApi/NetIAM.AdminApi.csproj
   dotnet run --project src/NetIAM.PortalApi/NetIAM.PortalApi.csproj
   dotnet run --project src/NetIAM.SyncWorker/NetIAM.SyncWorker.csproj
   ```

5. Run frontends:

   ```bash
   cd web/admin && npm install && npm run dev
   cd web/portal && npm install && npm run dev
   ```

## Default Seed

Seeded by `INetIamDataSeeder`:

- tenant identifier: `default`
- tenant id: `tenant-default`
- admin username: `admin`
- admin password: `NetIAM.Admin#2026`

## Notes

- API tenant context is resolved from `X-Tenant-Id` request header.
- OpenIddict client bootstrap endpoint: `POST /api/bootstrap/oidc/clients` on AuthServer.
