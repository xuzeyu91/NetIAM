using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using NetIAM.Infrastructure.Services;

namespace NetIAM.Infrastructure.Authorization;

public static class SessionRevocationDefaults
{
    public const string SessionIdHeader = "X-Session-Id";
}

public sealed class SessionRevocationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        ITenantContextAccessor tenantContextAccessor,
        ISessionRevocationService sessionRevocationService)
    {
        var sessionId = ResolveSessionId(httpContext);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            await next(httpContext);
            return;
        }

        var tenantId = tenantContextAccessor.GetTenantId();
        if (!await sessionRevocationService.IsRevokedAsync(tenantId, sessionId, httpContext.RequestAborted))
        {
            await next(httpContext);
            return;
        }

        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(
                new
                {
                    error = "session_revoked",
                    message = "Session has been revoked. Please login again."
                }),
            httpContext.RequestAborted);
    }

    private static string ResolveSessionId(HttpContext context)
    {
        var claimSid = context.User.FindFirstValue("sid");
        if (!string.IsNullOrWhiteSpace(claimSid))
        {
            return claimSid;
        }

        if (context.Request.Headers.TryGetValue(SessionRevocationDefaults.SessionIdHeader, out var value))
        {
            var headerSid = value.ToString();
            if (!string.IsNullOrWhiteSpace(headerSid))
            {
                return headerSid;
            }
        }

        return string.Empty;
    }
}

public static class SessionRevocationMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionRevocationGuard(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SessionRevocationMiddleware>();
    }
}
