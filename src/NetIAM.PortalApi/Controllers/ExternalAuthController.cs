using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Contracts;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Identity;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;
using NetIAM.Integrations.Abstractions;
using NetIAM.PortalApi.Services;

namespace NetIAM.PortalApi.Controllers;

[ApiController]
[Route("")]
public sealed class ExternalAuthController(
    NetIamDbContext dbContext,
    UserManager<NetIamIdentityUser> userManager,
    SignInManager<NetIamIdentityUser> signInManager,
    IExternalAuthProviderFactory providerFactory,
    IExternalAuthStateStore authStateStore,
    IAccountBindingService accountBindingService,
    ITenantContextAccessor tenantContextAccessor,
    IAuditService auditService) : ControllerBase
{
    public sealed record BindRequest(string ThirdPartyUserId, string Username, string Password);

    [HttpGet("authn/{provider}/{code}")]
    public async Task<IActionResult> AuthorizeEntry(
        string provider,
        string code,
        [FromQuery] bool asJson = false,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var providerType = ParseProviderType(provider);
        var providerEntity = await dbContext.IdentityProviders.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Code == code && x.ProviderType == providerType && x.Enabled && !x.IsDeleted,
            cancellationToken);

        if (providerEntity is null)
        {
            return NotFound($"Identity provider not found: {provider}/{code}.");
        }

        var state = await authStateStore.CreateStateAsync(tenantId, code, cancellationToken);
        var callbackBaseUri = $"{Request.Scheme}://{Request.Host}";
        var authorizeUrl = await providerFactory.Resolve(providerType).BuildAuthorizeUrlAsync(
            providerEntity,
            new ExternalAuthRequest(tenantId, code, callbackBaseUri, state),
            cancellationToken);

        if (asJson)
        {
            return Ok(new { authorizeUrl, state });
        }

        return Redirect(authorizeUrl);
    }

    [HttpGet("login/{provider}/{code}")]
    public async Task<IActionResult> Callback(
        string provider,
        string code,
        [FromQuery] string state,
        [FromQuery] string? authCode,
        [FromQuery(Name = "auth_code")] string? authCodeSnakeCase,
        [FromQuery] string? codeValue,
        [FromQuery(Name = "code")] string? oauthCode,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var providerType = ParseProviderType(provider);

        var consumed = await authStateStore.ConsumeStateAsync(tenantId, code, state, cancellationToken);
        if (!consumed)
        {
            return BadRequest("Invalid or expired state.");
        }

        var providerEntity = await dbContext.IdentityProviders.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Code == code && x.ProviderType == providerType && x.Enabled && !x.IsDeleted,
            cancellationToken);
        if (providerEntity is null)
        {
            return NotFound($"Identity provider not found: {provider}/{code}.");
        }

        var authorizationCode = authCode ?? authCodeSnakeCase ?? codeValue ?? oauthCode;
        if (string.IsNullOrWhiteSpace(authorizationCode))
        {
            return BadRequest("Missing authorization code.");
        }

        var handler = providerFactory.Resolve(providerType);
        var callbackUri = $"{Request.Scheme}://{Request.Host}/login/{provider}/{code}";
        var callback = new ExternalAuthCallback(tenantId, code, authorizationCode, state, callbackUri);
        var token = await handler.ExchangeTokenAsync(providerEntity, callback, cancellationToken);
        var profile = await handler.GetUserProfileAsync(providerEntity, token, cancellationToken);

        var bindResult = await accountBindingService.BindOrResolveAsync(
            tenantId,
            providerEntity.Id,
            profile,
            cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "portal.idp.login.callback",
                $"Provider {provider}/{code} login callback processed.",
                TargetJson: profile.RawProfileJson),
            cancellationToken);

        if (bindResult.Status == AccountBindingStatus.PendingBinding)
        {
            return Accepted(new
            {
                status = "pending_binding",
                bindResult.ThirdPartyUserId,
                hint = "Use POST /login/bind to bind an existing account."
            });
        }

        return Ok(new
        {
            status = bindResult.Status.ToString().ToLowerInvariant(),
            bindResult.UserId,
            bindResult.ThirdPartyUserId
        });
    }

    [HttpPost("login/bind")]
    public async Task<IActionResult> Bind([FromBody] BindRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var user = await userManager.Users
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UserName == request.Username && !x.IsDeleted, cancellationToken);
        if (user is null)
        {
            return BadRequest("User not found.");
        }

        var passwordResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!passwordResult.Succeeded)
        {
            return Unauthorized();
        }

        var thirdParty = await dbContext.ThirdPartyUsers
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.ThirdPartyUserId, cancellationToken);
        if (thirdParty is null)
        {
            return NotFound("Third-party account not found.");
        }

        var existing = await dbContext.UserIdpBinds
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                     && x.UserId == user.Id
                     && x.ThirdPartyUserId == thirdParty.Id,
                cancellationToken);

        if (existing is null)
        {
            dbContext.UserIdpBinds.Add(new()
            {
                TenantId = tenantId,
                UserId = user.Id,
                ThirdPartyUserId = thirdParty.Id,
                BoundTime = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "portal.idp.account.bound",
                $"Bound third-party account to {user.UserName}.",
                TargetJson: $$"""{"userId":"{{user.Id}}","thirdPartyUserId":"{{thirdParty.Id}}"}"""),
            cancellationToken);

        return Ok(new { status = "bound", userId = user.Id });
    }

    private static ExternalProviderType ParseProviderType(string provider)
    {
        return provider.ToLowerInvariant() switch
        {
            "dingtalk" => ExternalProviderType.DingTalk,
            "dingtalk_oauth" => ExternalProviderType.DingTalk,
            "feishu" => ExternalProviderType.Feishu,
            "feishu_oauth" => ExternalProviderType.Feishu,
            "wecom" => ExternalProviderType.WeCom,
            "wechatwork" => ExternalProviderType.WeCom,
            "wechat_work" => ExternalProviderType.WeCom,
            "wechatwork_oauth" => ExternalProviderType.WeCom,
            _ => throw new InvalidOperationException($"Unsupported provider type: {provider}.")
        };
    }
}
