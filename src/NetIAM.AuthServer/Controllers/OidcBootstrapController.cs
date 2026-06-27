using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

namespace NetIAM.AuthServer.Controllers;

[ApiController]
[Route("api/bootstrap/oidc")]
public sealed class OidcBootstrapController(IOpenIddictApplicationManager applicationManager) : ControllerBase
{
    public sealed record CreateClientRequest(
        string ClientId,
        string ClientSecret,
        string DisplayName,
        string[] RedirectUris);

    [HttpPost("clients")]
    public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            return BadRequest("clientId and clientSecret are required.");
        }

        var existing = await applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken);
        if (existing is not null)
        {
            return Conflict($"OIDC client '{request.ClientId}' already exists.");
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = request.ClientId,
            ClientSecret = request.ClientSecret,
            DisplayName = request.DisplayName,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.Endpoints.Introspection,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OpenId,
                OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Profile,
                OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.Email,
                OpenIddictConstants.Permissions.Prefixes.Scope + "netiam.api"
            }
        };

        foreach (var redirectUri in request.RedirectUris.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            descriptor.RedirectUris.Add(new Uri(redirectUri, UriKind.Absolute));
        }

        await applicationManager.CreateAsync(descriptor, cancellationToken);
        return Ok(new { request.ClientId, message = "OIDC client created." });
    }
}
