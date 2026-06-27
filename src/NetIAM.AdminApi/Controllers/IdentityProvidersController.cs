using System.Text.Json;
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
        string? ConfigJson,
        bool Enabled = true);

    public sealed record UpdateIdentityProviderRequest(
        string Name,
        ExternalProviderType ProviderType,
        bool Enabled,
        string? ConfigJson);

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
        var existing = await dbContext.IdentityProviders
            .AnyAsync(x => x.TenantId == tenantId && x.Code == request.Code && !x.IsDeleted, cancellationToken);
        if (existing)
        {
            return Conflict($"Identity provider code already exists: {request.Code}.");
        }

        if (!TryValidateAndNormalizeProviderConfig(request.ProviderType, request.ConfigJson, out var normalizedConfig, out var error))
        {
            return BadRequest(error);
        }

        var entity = new IdentityProviderEntity
        {
            TenantId = tenantId,
            Code = request.Code,
            Name = request.Name,
            ProviderType = request.ProviderType,
            ConfigJson = normalizedConfig,
            Enabled = request.Enabled
        };
        dbContext.IdentityProviders.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.identity-provider.created",
                $"Identity provider {request.Code} created.",
                TargetJson: normalizedConfig),
            cancellationToken);

        return Ok(entity);
    }

    [HttpPut("{code}")]
    [RequirePermission("provider.write")]
    public async Task<IActionResult> Update(string code, [FromBody] UpdateIdentityProviderRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = await dbContext.IdentityProviders
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!TryValidateAndNormalizeProviderConfig(request.ProviderType, request.ConfigJson, out var normalizedConfig, out var error))
        {
            return BadRequest(error);
        }

        entity.Name = request.Name;
        entity.ProviderType = request.ProviderType;
        entity.Enabled = request.Enabled;
        entity.ConfigJson = normalizedConfig;
        entity.UpdateTime = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.identity-provider.updated",
                $"Identity provider {code} updated.",
                TargetJson: normalizedConfig),
            cancellationToken);

        return Ok(entity);
    }

    [HttpDelete("{code}")]
    [RequirePermission("provider.write")]
    public async Task<IActionResult> Delete(string code, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = await dbContext.IdentityProviders
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
                "admin.identity-provider.deleted",
                $"Identity provider {code} deleted."),
            cancellationToken);

        return NoContent();
    }

    private static bool TryValidateAndNormalizeProviderConfig(
        ExternalProviderType providerType,
        string? configJson,
        out string normalizedConfig,
        out string error)
    {
        normalizedConfig = "{}";
        error = string.Empty;
        if (!TryParseJsonObject(configJson, out var config, out error))
        {
            return false;
        }

        switch (providerType)
        {
            case ExternalProviderType.DingTalk:
                if (!HasAnyNonEmpty(config, "appKey", "appId", "clientId"))
                {
                    error = "DingTalk config requires appKey (or appId/clientId).";
                    return false;
                }

                if (!HasAnyNonEmpty(config, "appSecret", "clientSecret"))
                {
                    error = "DingTalk config requires appSecret (or clientSecret).";
                    return false;
                }

                break;
            case ExternalProviderType.Feishu:
                if (!HasAnyNonEmpty(config, "appId", "clientId"))
                {
                    error = "Feishu config requires appId (or clientId).";
                    return false;
                }

                if (!HasAnyNonEmpty(config, "appSecret", "clientSecret"))
                {
                    error = "Feishu config requires appSecret (or clientSecret).";
                    return false;
                }

                break;
            case ExternalProviderType.WeCom:
                if (!HasAnyNonEmpty(config, "corpId"))
                {
                    error = "WeCom config requires corpId.";
                    return false;
                }

                if (!HasAnyNonEmpty(config, "agentId"))
                {
                    error = "WeCom config requires agentId.";
                    return false;
                }

                if (!HasAnyNonEmpty(config, "appSecret", "corpSecret"))
                {
                    error = "WeCom config requires appSecret (or corpSecret).";
                    return false;
                }

                break;
            default:
                error = $"Unsupported provider type: {providerType}.";
                return false;
        }

        normalizedConfig = JsonSerializer.Serialize(config);
        return true;
    }

    private static bool TryParseJsonObject(string? json, out JsonElement element, out string error)
    {
        error = string.Empty;
        element = default;
        var rawJson = string.IsNullOrWhiteSpace(json) ? "{}" : json;

        try
        {
            element = JsonDocument.Parse(rawJson).RootElement.Clone();
        }
        catch (JsonException ex)
        {
            error = $"ConfigJson must be valid JSON: {ex.Message}";
            return false;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            error = "ConfigJson must be a JSON object.";
            return false;
        }

        return true;
    }

    private static bool HasAnyNonEmpty(JsonElement json, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (json.TryGetProperty(propertyName, out var value) && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return true;
            }
        }

        return false;
    }
}
