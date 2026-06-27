using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Infrastructure.Identity;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AuthServer.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class LocalAuthController(
    UserManager<NetIamIdentityUser> userManager,
    IDataProtectionProvider dataProtectionProvider,
    ITenantContextAccessor tenantContextAccessor,
    IAuditService auditService) : ControllerBase
{
    private readonly IDataProtector _ticketProtector = dataProtectionProvider.CreateProtector("NetIAM.Auth.LocalLoginTicket.v1");

    public sealed record LocalLoginRequest(string Username, string Password);

    [HttpPost("local-login")]
    public async Task<IActionResult> LocalLogin([FromBody] LocalLoginRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var user = await userManager.Users.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.UserName == request.Username && !x.IsDeleted,
            cancellationToken);

        if (user is null)
        {
            await auditService.WriteAsync(
                new AuditWriteRequest(tenantId, "auth.local-login.failed", $"User {request.Username} not found.", "failed"),
                cancellationToken);
            return Unauthorized();
        }

        var passwordOk = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordOk)
        {
            await userManager.AccessFailedAsync(user);
            await auditService.WriteAsync(
                new AuditWriteRequest(tenantId, "auth.local-login.failed", $"User {request.Username} password incorrect.", "failed", ActorType: "user", ActorId: user.Id),
                cancellationToken);
            return Unauthorized();
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var sessionId = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.AddHours(8);
        var ticketPayload = $"{tenantId}|{user.Id}|{expiresAt:O}|{sessionId}";
        var loginTicket = _ticketProtector.Protect(ticketPayload);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "auth.local-login.succeeded",
                $"User {request.Username} login succeeded.",
                ActorType: "user",
                ActorId: user.Id,
                RequestId: HttpContext.TraceIdentifier,
                SessionId: sessionId,
                UserAgent: Request.Headers.UserAgent.ToString(),
                IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);

        return Ok(new
        {
            userId = user.Id,
            username = user.UserName,
            displayName = user.DisplayName,
            sessionId,
            expiresAt,
            loginTicket
        });
    }
}
