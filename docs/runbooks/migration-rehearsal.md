# Migration Rehearsal Runbook

## Goal

Validate that NetIAM can complete an end-to-end rehearsal from empty database to key IAM flows.

## Preconditions

- PostgreSQL and Redis are running (`deploy/docker-compose.yml`)
- .NET 10 SDK and Node.js are installed
- no local config override blocks default connection strings

## Rehearsal Steps

1. Build all backend projects:

   ```bash
   dotnet build src/NetIAM.sln
   ```

2. Apply schema migration:

   ```bash
   dotnet ef database update --project src/NetIAM.Infrastructure/NetIAM.Infrastructure.csproj
   ```

3. Start services (4 terminals):

   ```bash
   dotnet run --project src/NetIAM.AuthServer/NetIAM.AuthServer.csproj
   dotnet run --project src/NetIAM.AdminApi/NetIAM.AdminApi.csproj
   dotnet run --project src/NetIAM.PortalApi/NetIAM.PortalApi.csproj
   dotnet run --project src/NetIAM.SyncWorker/NetIAM.SyncWorker.csproj
   ```

4. Verify health endpoints:
   - `GET https://localhost:7001/api/health`
   - `GET https://localhost:7002/api/health`
   - `GET https://localhost:7003/api/health`

5. Validate local identity baseline:
   - seed account login via `POST https://localhost:7001/api/auth/local-login`
   - list users via `GET https://localhost:7002/api/admin/users`
   - create a tenant/org/provider/source via admin APIs

6. Validate provider + bind flow:
   - trigger `/authn/{provider}/{code}?asJson=true`
   - manually execute callback with test `code/state` in dev mode
   - if pending binding, call `POST /login/bind`

7. Validate sync flow:
   - trigger `POST /api/admin/identity-sources/{code}/sync`
   - send webhook payload to `POST /api/v1/synchronizer/event/{code}`
   - check audit and sync records

8. Validate frontend builds:

   ```bash
   cd web/admin && npm run build
   cd web/portal && npm run build
   ```

## Exit Criteria

- backend build and migration succeed without manual SQL patching
- all health checks return `healthy`
- user/provider/source CRUD works from admin APIs
- callback and bind flow returns expected status
- sync trigger and webhook endpoint write records
- both frontends build successfully
