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
        string? BasicConfigJson,
        string? StrategyConfigJson = null,
        string? JobConfigJson = null);

    public sealed record UpdateIdentitySourceRequest(
        string Name,
        IdentitySourceProviderType ProviderType,
        bool Enabled,
        string? BasicConfigJson,
        string? StrategyConfigJson = null,
        string? JobConfigJson = null);

    [HttpGet]
    [RequirePermission("source.read")]
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
    [RequirePermission("source.write")]
    public async Task<IActionResult> Create([FromBody] CreateIdentitySourceRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var existing = await dbContext.IdentitySources
            .AnyAsync(x => x.TenantId == tenantId && x.Code == request.Code && !x.IsDeleted, cancellationToken);
        if (existing)
        {
            return Conflict($"Identity source code already exists: {request.Code}.");
        }

        if (!TryValidateAndNormalizeSourceConfig(request.ProviderType, request.BasicConfigJson, out var basicConfig, out var error))
        {
            return BadRequest(error);
        }

        if (!TryParseJsonObject(request.StrategyConfigJson, out var strategyConfigElement, out error))
        {
            return BadRequest($"StrategyConfigJson invalid: {error}");
        }

        if (!TryParseJsonObject(request.JobConfigJson, out var jobConfigElement, out error))
        {
            return BadRequest($"JobConfigJson invalid: {error}");
        }

        var entity = new IdentitySourceEntity
        {
            TenantId = tenantId,
            Code = request.Code,
            Name = request.Name,
            ProviderType = request.ProviderType,
            Enabled = true,
            BasicConfigJson = basicConfig,
            StrategyConfigJson = JsonSerializer.Serialize(strategyConfigElement),
            JobConfigJson = JsonSerializer.Serialize(jobConfigElement)
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

    [HttpPut("{code}")]
    [RequirePermission("source.write")]
    public async Task<IActionResult> Update(string code, [FromBody] UpdateIdentitySourceRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = await dbContext.IdentitySources
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code && !x.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        if (!TryValidateAndNormalizeSourceConfig(request.ProviderType, request.BasicConfigJson, out var basicConfig, out var error))
        {
            return BadRequest(error);
        }

        if (!TryParseJsonObject(request.StrategyConfigJson, out var strategyConfigElement, out error))
        {
            return BadRequest($"StrategyConfigJson invalid: {error}");
        }

        if (!TryParseJsonObject(request.JobConfigJson, out var jobConfigElement, out error))
        {
            return BadRequest($"JobConfigJson invalid: {error}");
        }

        entity.Name = request.Name;
        entity.ProviderType = request.ProviderType;
        entity.Enabled = request.Enabled;
        entity.BasicConfigJson = basicConfig;
        entity.StrategyConfigJson = JsonSerializer.Serialize(strategyConfigElement);
        entity.JobConfigJson = JsonSerializer.Serialize(jobConfigElement);
        entity.UpdateTime = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.identity-source.updated",
                $"Identity source {code} updated."),
            cancellationToken);

        return Ok(entity);
    }

    [HttpDelete("{code}")]
    [RequirePermission("source.write")]
    public async Task<IActionResult> Delete(string code, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var entity = await dbContext.IdentitySources
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
                "admin.identity-source.deleted",
                $"Identity source {code} deleted."),
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{code}/sync")]
    [RequirePermission("source.write")]
    public async Task<IActionResult> RunSync(string code, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var result = await directorySyncService.RunFullSyncAsync(code, tenantId, cancellationToken);
        return Ok(result);
    }

    private static bool TryValidateAndNormalizeSourceConfig(
        IdentitySourceProviderType providerType,
        string? basicConfigJson,
        out string normalizedConfig,
        out string error)
    {
        normalizedConfig = "{}";
        error = string.Empty;
        if (!TryParseJsonObject(basicConfigJson, out var config, out error))
        {
            return false;
        }

        var useMock = TryGetBoolean(config, "useMock", false);
        if (useMock)
        {
            normalizedConfig = JsonSerializer.Serialize(config);
            return true;
        }

        switch (providerType)
        {
            case IdentitySourceProviderType.DingTalk:
                if (!HasAnyNonEmpty(config, "appKey"))
                {
                    error = "DingTalk identity source requires appKey.";
                    return false;
                }

                if (!HasAnyNonEmpty(config, "appSecret"))
                {
                    error = "DingTalk identity source requires appSecret.";
                    return false;
                }

                break;
            case IdentitySourceProviderType.Feishu:
                if (!HasAnyNonEmpty(config, "appId"))
                {
                    error = "Feishu identity source requires appId.";
                    return false;
                }

                if (!HasAnyNonEmpty(config, "appSecret"))
                {
                    error = "Feishu identity source requires appSecret.";
                    return false;
                }

                break;
            case IdentitySourceProviderType.WeCom:
                if (!HasAnyNonEmpty(config, "corpId"))
                {
                    error = "WeCom identity source requires corpId.";
                    return false;
                }

                if (!HasAnyNonEmpty(config, "appSecret", "corpSecret"))
                {
                    error = "WeCom identity source requires appSecret (or corpSecret).";
                    return false;
                }

                break;
            default:
                error = $"Unsupported identity source type: {providerType}.";
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
            error = ex.Message;
            return false;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            error = "must be a JSON object";
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

    private static bool TryGetBoolean(JsonElement json, string propertyName, bool fallback)
    {
        if (!json.TryGetProperty(propertyName, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => fallback
        };
    }
}
