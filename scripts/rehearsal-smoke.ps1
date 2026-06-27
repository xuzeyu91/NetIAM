param(
    [string]$AuthServer = "https://localhost:7001",
    [string]$AdminApi = "https://localhost:7002",
    [string]$PortalApi = "https://localhost:7003"
)

$ErrorActionPreference = "Stop"

Write-Host "== NetIAM rehearsal smoke check =="
Write-Host "Building backend..."
dotnet build "src/NetIAM.sln"

Write-Host "Applying database migration..."
dotnet ef database update --project "src/NetIAM.Infrastructure/NetIAM.Infrastructure.csproj"

Write-Host "Checking health endpoints..."
$auth = Invoke-RestMethod -Uri "$AuthServer/api/health" -Method Get
$admin = Invoke-RestMethod -Uri "$AdminApi/api/health" -Method Get
$portal = Invoke-RestMethod -Uri "$PortalApi/api/health" -Method Get

Write-Host "AuthServer: $($auth.status)"
Write-Host "AdminApi : $($admin.status)"
Write-Host "PortalApi : $($portal.status)"

Write-Host "Building frontend projects..."
Push-Location "web/admin"
npm run build
Pop-Location

Push-Location "web/portal"
npm run build
Pop-Location

Write-Host "Smoke rehearsal finished."
