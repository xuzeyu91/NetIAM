using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.AdminApi.Scim;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Identity;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[ScimTokenAuthorize]
[Route("scim/v2/Users")]
public sealed class ScimUsersController(UserManager<NetIamIdentityUser> userManager) : ControllerBase
{
    public sealed record ScimUserEmail(string Value, bool Primary = false);
    public sealed record ScimPhoneNumber(string Value);
    public sealed record ScimUserName(string? Formatted);
    public sealed record ScimCreateUserRequest(
        string UserName,
        string? DisplayName,
        bool? Active,
        ScimUserName? Name,
        IReadOnlyCollection<ScimUserEmail>? Emails,
        IReadOnlyCollection<ScimPhoneNumber>? PhoneNumbers);

    public sealed record ScimPatchRequest(IReadOnlyCollection<ScimPatchOperation> Operations);
    public sealed record ScimPatchOperation(string Op, string? Path, JsonElement Value);

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int startIndex = 1,
        [FromQuery] int count = 50,
        [FromQuery] string? filter = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        var normalizedStart = Math.Max(1, startIndex);
        var normalizedCount = Math.Clamp(count, 1, 200);

        var query = userManager.Users.Where(x => x.TenantId == tenantId);
        query = ApplyFilter(query, filter);

        var totalResults = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(x => x.UserName)
            .Skip(normalizedStart - 1)
            .Take(normalizedCount)
            .ToListAsync(cancellationToken);

        return Ok(
            ScimResponseFactory.BuildListResponse(
                users.Select(ScimResponseFactory.BuildUser).ToArray(),
                totalResults,
                normalizedStart,
                users.Count));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var user = await userManager.Users
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(ScimResponseFactory.BuildUser(user));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ScimCreateUserRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var existing = await userManager.Users.AnyAsync(x => x.TenantId == tenantId && x.UserName == request.UserName, cancellationToken);
        if (existing)
        {
            return Conflict(new { detail = "userName already exists." });
        }

        var user = new NetIamIdentityUser
        {
            TenantId = tenantId,
            UserName = request.UserName,
            DisplayName = request.DisplayName
                          ?? request.Name?.Formatted
                          ?? request.UserName,
            Email = request.Emails?.FirstOrDefault()?.Value,
            PhoneNumber = request.PhoneNumbers?.FirstOrDefault()?.Value,
            IsDeleted = request.Active == false,
            DataOrigin = DataOriginType.Local
        };

        var password = $"Scim#{Guid.NewGuid():N}A1!";
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return BadRequest(new { detail = string.Join("; ", result.Errors.Select(x => x.Description)) });
        }

        return Created($"/scim/v2/Users/{user.Id}", ScimResponseFactory.BuildUser(user));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Replace(string id, [FromBody] ScimCreateUserRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var user = await userManager.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.UserName = request.UserName;
        user.DisplayName = request.DisplayName
                           ?? request.Name?.Formatted
                           ?? request.UserName;
        user.Email = request.Emails?.FirstOrDefault()?.Value;
        user.PhoneNumber = request.PhoneNumbers?.FirstOrDefault()?.Value;
        user.IsDeleted = request.Active == false;
        user.UpdateTime = DateTimeOffset.UtcNow;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { detail = string.Join("; ", result.Errors.Select(x => x.Description)) });
        }

        return Ok(ScimResponseFactory.BuildUser(user));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(string id, [FromBody] ScimPatchRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var user = await userManager.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        foreach (var operation in request.Operations)
        {
            if (!string.Equals(operation.Op, "replace", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(operation.Op, "add", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = operation.Path?.ToLowerInvariant() ?? string.Empty;
            if (path is "displayname")
            {
                user.DisplayName = operation.Value.GetString() ?? user.DisplayName;
            }
            else if (path is "active")
            {
                user.IsDeleted = !(operation.Value.GetBoolean());
            }
            else if (path.StartsWith("emails"))
            {
                if (operation.Value.ValueKind == JsonValueKind.Array)
                {
                    user.Email = ExtractFirstArrayValue(operation.Value) ?? user.Email;
                }
            }
            else if (path.StartsWith("phonenumbers"))
            {
                if (operation.Value.ValueKind == JsonValueKind.Array)
                {
                    user.PhoneNumber = ExtractFirstArrayValue(operation.Value) ?? user.PhoneNumber;
                }
            }
        }

        user.UpdateTime = DateTimeOffset.UtcNow;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(new { detail = string.Join("; ", result.Errors.Select(x => x.Description)) });
        }

        return Ok(ScimResponseFactory.BuildUser(user));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var user = await userManager.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.IsDeleted = true;
        user.UpdateTime = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);
        return NoContent();
    }

    private string ResolveTenantId()
    {
        var principal = HttpContext.GetScimPrincipal();
        if (principal is null)
        {
            throw new UnauthorizedAccessException("SCIM token principal missing.");
        }

        return principal.TenantId;
    }

    private static IQueryable<NetIamIdentityUser> ApplyFilter(IQueryable<NetIamIdentityUser> query, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return query;
        }

        var normalized = filter.Trim();
        if (normalized.StartsWith("userName eq ", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractFilterValue(normalized["userName eq ".Length..]);
            return query.Where(x => x.UserName == value);
        }

        if (normalized.StartsWith("id eq ", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractFilterValue(normalized["id eq ".Length..]);
            return query.Where(x => x.Id == value);
        }

        return query;
    }

    private static string ExtractFilterValue(string raw)
    {
        return raw.Trim().Trim('"');
    }

    private static string? ExtractFirstArrayValue(JsonElement element)
    {
        var first = element.EnumerateArray().FirstOrDefault();
        return first.TryGetProperty("value", out var property) ? property.GetString() : null;
    }
}
