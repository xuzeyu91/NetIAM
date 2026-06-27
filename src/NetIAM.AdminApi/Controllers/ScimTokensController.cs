using Microsoft.AspNetCore.Mvc;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/scim/tokens")]
public sealed class ScimTokensController(
    IScimTokenService scimTokenService,
    ITenantContextAccessor tenantContextAccessor,
    IAuditService auditService) : ControllerBase
{
    public sealed record CreateScimTokenRequest(string Name, int ExpiresInDays = 365);

    [HttpGet]
    [RequirePermission("scim.read")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var tokens = await scimTokenService.ListTokensAsync(tenantId, cancellationToken);
        return Ok(tokens.Select(x => new
        {
            x.Id,
            x.Name,
            x.IsActive,
            x.ExpiresTime,
            x.LastUsedTime,
            x.CreateTime
        }));
    }

    [HttpPost]
    [RequirePermission("scim.write")]
    public async Task<IActionResult> Create([FromBody] CreateScimTokenRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var (entity, plainToken) = await scimTokenService.CreateTokenAsync(tenantId, request.Name, request.ExpiresInDays, cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.scim.token.created",
                $"SCIM token {request.Name} created.",
                TargetJson: $$"""{"tokenId":"{{entity.Id}}"}"""),
            cancellationToken);

        return Ok(new
        {
            entity.Id,
            entity.Name,
            entity.ExpiresTime,
            token = plainToken
        });
    }

    [HttpDelete("{id}")]
    [RequirePermission("scim.write")]
    public async Task<IActionResult> Revoke(string id, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var revoked = await scimTokenService.RevokeTokenAsync(tenantId, id, cancellationToken);
        if (!revoked)
        {
            return NotFound();
        }

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.scim.token.revoked",
                $"SCIM token {id} revoked."),
            cancellationToken);

        return NoContent();
    }
}
