using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NetIAM.Infrastructure.Services;

namespace NetIAM.Infrastructure.Authorization;

public static class RbacAuthorizationDefaults
{
    public const string PermissionPolicyPrefix = "perm:";
    public const string ActingUserHeader = "X-Acting-User-Id";
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permissionCode)
    {
        Policy = $"{RbacAuthorizationDefaults.PermissionPolicyPrefix}{permissionCode}";
    }
}

public sealed class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}

public sealed class RbacPermissionAuthorizationHandler(
    IRbacService rbacService,
    ITenantContextAccessor tenantContextAccessor,
    IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var userId = ResolveUserId(context.User, httpContextAccessor.HttpContext);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        if (await rbacService.UserHasPermissionAsync(tenantId, userId, requirement.PermissionCode))
        {
            context.Succeed(requirement);
        }
    }

    private static string? ResolveUserId(ClaimsPrincipal user, HttpContext? httpContext)
    {
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue("sub")
               ?? httpContext?.Request.Headers[RbacAuthorizationDefaults.ActingUserHeader].ToString();
    }
}

public sealed class RbacPermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(RbacAuthorizationDefaults.PermissionPolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permissionCode = policyName[RbacAuthorizationDefaults.PermissionPolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(permissionCode))
                .Build();
            return policy;
        }

        return await base.GetPolicyAsync(policyName);
    }
}
