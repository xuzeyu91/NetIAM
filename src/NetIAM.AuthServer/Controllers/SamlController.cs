using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using NetIAM.Domain.Contracts;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AuthServer.Controllers;

[ApiController]
[Route("saml2")]
public sealed class SamlController(
    ISamlService samlService,
    ITenantContextAccessor tenantContextAccessor,
    IAuditService auditService) : ControllerBase
{
    [HttpGet("metadata/{tenantId?}")]
    public async Task<IActionResult> Metadata(string? tenantId, CancellationToken cancellationToken)
    {
        var resolvedTenantId = string.IsNullOrWhiteSpace(tenantId)
            ? tenantContextAccessor.GetTenantId()
            : tenantId;
        var baseUri = $"{Request.Scheme}://{Request.Host}";

        var xml = await samlService.BuildMetadataXmlAsync(resolvedTenantId, baseUri, cancellationToken);
        return Content(xml, "application/samlmetadata+xml", Encoding.UTF8);
    }

    [HttpGet("sso/{serviceProviderCode}")]
    public async Task<IActionResult> Sso(
        string serviceProviderCode,
        [FromQuery] string? userId,
        [FromQuery] string? relayState,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var resolvedUserId =
            userId
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? Request.Headers["X-Acting-User-Id"].ToString();
        if (string.IsNullOrWhiteSpace(resolvedUserId))
        {
            return BadRequest("Missing userId. Supply query userId or authenticated subject.");
        }

        var html = await samlService.BuildHttpPostHtmlAsync(
            new SamlSsoRequest(tenantId, serviceProviderCode, resolvedUserId, relayState),
            cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "auth.saml.sso.issued",
                $"SAML SSO response issued for SP {serviceProviderCode}.",
                ActorType: "user",
                ActorId: resolvedUserId),
            cancellationToken);

        return Content(html, "text/html", Encoding.UTF8);
    }

    [HttpPost("acs/{serviceProviderCode}")]
    public async Task<IActionResult> Acs(
        string serviceProviderCode,
        [FromForm] string? SAMLResponse,
        [FromForm] string? RelayState,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        if (string.IsNullOrWhiteSpace(SAMLResponse))
        {
            return BadRequest("SAMLResponse is required.");
        }

        string preview;
        try
        {
            var bytes = Convert.FromBase64String(SAMLResponse);
            preview = Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            preview = "Invalid Base64 SAMLResponse.";
        }

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "auth.saml.acs.received",
                $"SAML ACS response received for SP {serviceProviderCode}.",
                TargetJson: $$"""{"relayState":"{{RelayState ?? string.Empty}}"}"""),
            cancellationToken);

        return Ok(new
        {
            accepted = true,
            serviceProviderCode,
            relayState = RelayState,
            preview = preview.Length > 500 ? $"{preview[..500]}..." : preview
        });
    }
}
