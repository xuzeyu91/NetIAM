using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NetIAM.Domain.Contracts;
using NetIAM.Infrastructure.Services;

namespace NetIAM.Infrastructure.Authorization;

public static class ScimHttpContextExtensions
{
    public const string ScimPrincipalItemKey = "netiam.scim.principal";

    public static ScimPrincipalContext? GetScimPrincipal(this HttpContext httpContext)
    {
        return httpContext.Items.TryGetValue(ScimPrincipalItemKey, out var value)
            ? value as ScimPrincipalContext
            : null;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ScimTokenAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var tokenService = context.HttpContext.RequestServices.GetRequiredService<IScimTokenService>();

        var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var plainToken = authorization["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(plainToken))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var principal = await tokenService.ValidateAsync(plainToken, context.HttpContext.TraceIdentifier);
        if (principal is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        context.HttpContext.Items[ScimHttpContextExtensions.ScimPrincipalItemKey] = principal;
    }
}
