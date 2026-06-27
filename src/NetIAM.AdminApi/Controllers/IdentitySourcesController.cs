using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/identity-sources")]
public sealed class IdentitySourcesController(
    NetIamDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    IDirectorySyncService directorySyncService,
    IAuditService auditService) : ControllerBase
{
    public sealed record CreateIdentitySourceRequest(
        string Code,
        string Name,
        IdentitySourceProviderType ProviderType,
        string BasicConfigJson,
        string? StrategyConfigJson = null,
        string? JobConfigJson = null);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var sources = await dbContext.IdentitySources
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.ProviderType)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);
        return Ok(sources);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIdentitySourceRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = new IdentitySourceEntity
        {
            TenantId = tenantId,
            Code = request.Code,
            Name = request.Name,
            ProviderType = request.ProviderType,
            Enabled = true,
            BasicConfigJson = string.IsNullOrWhiteSpace(request.BasicConfigJson) ? "{}" : request.BasicConfigJson,
            StrategyConfigJson = string.IsNullOrWhiteSpace(request.StrategyConfigJson) ? "{}" : request.StrategyConfigJson,
            JobConfigJson = string.IsNullOrWhiteSpace(request.JobConfigJson) ? "{}" : request.JobConfigJson
        };
        dbContext.IdentitySources.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.identity-source.created",
                $"Identity source {request.Code} created."),
            cancellationToken);

        return Ok(entity);
    }

    [HttpPost("{code}/sync")]
    public async Task<IActionResult> RunSync(string code, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var result = await directorySyncService.RunFullSyncAsync(code, tenantId, cancellationToken);
        return Ok(result);
    }
}
