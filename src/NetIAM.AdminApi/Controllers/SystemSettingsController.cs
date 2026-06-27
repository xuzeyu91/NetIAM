using Microsoft.AspNetCore.Mvc;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/settings")]
public sealed class SystemSettingsController(
    ITenantContextAccessor tenantContextAccessor,
    ISystemSettingStore systemSettingStore,
    IAuditService auditService) : ControllerBase
{
    private const string MessageSettingKey = "system.message";
    private const string StorageSettingKey = "system.storage";
    private const string GeoIpSettingKey = "system.geoip";

    public sealed record MessageSettings(
        string EmailProvider = "smtp",
        string EmailFromAddress = "",
        string EmailHost = "",
        int EmailPort = 465,
        bool EmailUseSsl = true,
        string SmsProvider = "none",
        string SmsSignName = "",
        string SmsTemplateCode = "",
        string MailTemplate = "",
        string SmsTemplate = "");

    public sealed record StorageSettings(
        string Provider = "local",
        string Endpoint = "",
        string Bucket = "",
        string AccessKeyId = "",
        string SecretAccessKey = "",
        string Region = "");

    public sealed record GeoIpSettings(
        bool Enabled = false,
        string Provider = "builtin",
        string DatabasePath = "",
        string ApiEndpoint = "",
        string ApiToken = "");

    [HttpGet("message")]
    [RequirePermission("setting.read")]
    public async Task<IActionResult> GetMessageSettings(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var settings = await systemSettingStore.GetAsync(tenantId, MessageSettingKey, new MessageSettings(), cancellationToken);
        return Ok(settings);
    }

    [HttpPut("message")]
    [RequirePermission("setting.write")]
    public async Task<IActionResult> UpdateMessageSettings([FromBody] MessageSettings request, CancellationToken cancellationToken)
    {
        var validationError = ValidateMessage(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        await systemSettingStore.SetAsync(tenantId, MessageSettingKey, request, cancellationToken);
        await auditService.WriteAsync(
            new AuditWriteRequest(tenantId, "admin.settings.message.updated", "Message settings updated."),
            cancellationToken);
        return Ok(request);
    }

    [HttpGet("storage")]
    [RequirePermission("setting.read")]
    public async Task<IActionResult> GetStorageSettings(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var settings = await systemSettingStore.GetAsync(tenantId, StorageSettingKey, new StorageSettings(), cancellationToken);
        return Ok(settings);
    }

    [HttpPut("storage")]
    [RequirePermission("setting.write")]
    public async Task<IActionResult> UpdateStorageSettings([FromBody] StorageSettings request, CancellationToken cancellationToken)
    {
        var validationError = ValidateStorage(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        await systemSettingStore.SetAsync(tenantId, StorageSettingKey, request, cancellationToken);
        await auditService.WriteAsync(
            new AuditWriteRequest(tenantId, "admin.settings.storage.updated", "Storage settings updated."),
            cancellationToken);
        return Ok(request);
    }

    [HttpGet("geoip")]
    [RequirePermission("setting.read")]
    public async Task<IActionResult> GetGeoIpSettings(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var settings = await systemSettingStore.GetAsync(tenantId, GeoIpSettingKey, new GeoIpSettings(), cancellationToken);
        return Ok(settings);
    }

    [HttpPut("geoip")]
    [RequirePermission("setting.write")]
    public async Task<IActionResult> UpdateGeoIpSettings([FromBody] GeoIpSettings request, CancellationToken cancellationToken)
    {
        var validationError = ValidateGeoIp(request);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        await systemSettingStore.SetAsync(tenantId, GeoIpSettingKey, request, cancellationToken);
        await auditService.WriteAsync(
            new AuditWriteRequest(tenantId, "admin.settings.geoip.updated", "GeoIP settings updated."),
            cancellationToken);
        return Ok(request);
    }

    private static string? ValidateMessage(MessageSettings request)
    {
        if (request.EmailPort is < 1 or > 65535)
        {
            return "EmailPort must be between 1 and 65535.";
        }

        if (request.EmailProvider.Length > 64 || request.SmsProvider.Length > 64)
        {
            return "Provider value is too long.";
        }

        return null;
    }

    private static string? ValidateStorage(StorageSettings request)
    {
        if (string.IsNullOrWhiteSpace(request.Provider))
        {
            return "Provider is required.";
        }

        if (request.Provider.Length > 64)
        {
            return "Provider length exceeds limit.";
        }

        return null;
    }

    private static string? ValidateGeoIp(GeoIpSettings request)
    {
        if (string.IsNullOrWhiteSpace(request.Provider))
        {
            return "Provider is required.";
        }

        if (request.Provider.Length > 64)
        {
            return "Provider length exceeds limit.";
        }

        return null;
    }
}
