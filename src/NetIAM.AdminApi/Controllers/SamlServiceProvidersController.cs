using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/saml/service-providers")]
public sealed class SamlServiceProvidersController(
    NetIamDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    IAuditService auditService) : ControllerBase
{
    public sealed record UpsertSamlServiceProviderRequest(
        string Code,
        string Name,
        string EntityId,
        string AssertionConsumerServiceUrl,
        string? SingleLogoutServiceUrl,
        string? NameIdFormat,
        string? Audience,
        string? RelayStateDefault,
        bool WantSignedAssertions,
        bool AllowUnsolicitedResponse,
        SamlBindingType BindingType,
        bool Enabled,
        string? SigningCertificatePem);

    [HttpGet]
    [RequirePermission("saml.read")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var providers = await dbContext.SamlServiceProviders
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
        return Ok(providers);
    }

    [HttpPost]
    [RequirePermission("saml.write")]
    public async Task<IActionResult> Upsert([FromBody] UpsertSamlServiceProviderRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = await dbContext.SamlServiceProviders
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == request.Code && !x.IsDeleted, cancellationToken);

        if (entity is null)
        {
            entity = new SamlServiceProviderEntity
            {
                TenantId = tenantId,
                Code = request.Code
            };
            dbContext.SamlServiceProviders.Add(entity);
        }

        entity.Name = request.Name;
        entity.EntityId = request.EntityId;
        entity.AssertionConsumerServiceUrl = request.AssertionConsumerServiceUrl;
        entity.SingleLogoutServiceUrl = request.SingleLogoutServiceUrl;
        entity.NameIdFormat = string.IsNullOrWhiteSpace(request.NameIdFormat)
            ? "urn:oasis:names:tc:SAML:1.1:nameid-format:unspecified"
            : request.NameIdFormat;
        entity.Audience = request.Audience;
        entity.RelayStateDefault = request.RelayStateDefault;
        entity.WantSignedAssertions = request.WantSignedAssertions;
        entity.AllowUnsolicitedResponse = request.AllowUnsolicitedResponse;
        entity.BindingType = request.BindingType;
        entity.Enabled = request.Enabled;
        entity.SigningCertificatePem = request.SigningCertificatePem;
        entity.UpdateTime = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.saml.sp.upserted",
                $"SAML service provider {request.Code} upserted.",
                TargetJson: $$"""{"code":"{{request.Code}}"}"""),
            cancellationToken);

        return Ok(entity);
    }

    [HttpDelete("{code}")]
    [RequirePermission("saml.write")]
    public async Task<IActionResult> Delete(string code, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = await dbContext.SamlServiceProviders
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsDeleted = true;
        entity.UpdateTime = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.saml.sp.deleted",
                $"SAML service provider {code} deleted."),
            cancellationToken);

        return NoContent();
    }
}
