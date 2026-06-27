using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/identity-providers")]
public sealed class IdentityProvidersController(
    NetIamDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    IAuditService auditService) : ControllerBase
{
    public sealed record CreateIdentityProviderRequest(
        string Code,
        string Name,
        ExternalProviderType ProviderType,
        string ConfigJson);

    [HttpGet]
    [RequirePermission("provider.read")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var providers = await dbContext.IdentityProviders
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.ProviderType)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);
        return Ok(providers);
    }

    [HttpPost]
    [RequirePermission("provider.write")]
    public async Task<IActionResult> Create([FromBody] CreateIdentityProviderRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = new IdentityProviderEntity
        {
            TenantId = tenantId,
            Code = request.Code,
            Name = request.Name,
            ProviderType = request.ProviderType,
            ConfigJson = string.IsNullOrWhiteSpace(request.ConfigJson) ? "{}" : request.ConfigJson,
            Enabled = true
        };
        dbContext.IdentityProviders.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.identity-provider.created",
                $"Identity provider {request.Code} created.",
                TargetJson: request.ConfigJson),
            cancellationToken);

        return Ok(entity);
    }
}
