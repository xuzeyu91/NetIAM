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

    public sealed record ScimPatchRequest(IReadOnlyCollection<ScimPatchOperation>? Operations);
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

        var operations = request.Operations ?? Array.Empty<ScimPatchOperation>();
        foreach (var operation in operations)
        {
            var op = operation.Op?.Trim().ToLowerInvariant() ?? string.Empty;
            if (op is not ("replace" or "add" or "remove"))
            {
                continue;
            }

            var path = operation.Path?.Trim().ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path) && operation.Value.ValueKind == JsonValueKind.Object)
            {
                ApplyPatchObject(user, op, operation.Value);
            }
            else if (path is "displayname" or "name.formatted")
            {
                if (op == "remove")
                {
                    user.DisplayName = user.UserName ?? user.DisplayName;
                }
                else
                {
                    user.DisplayName = ExtractScalarString(operation.Value) ?? user.DisplayName;
                }
            }
            else if (path is "username")
            {
                if (op != "remove")
                {
                    user.UserName = ExtractScalarString(operation.Value) ?? user.UserName;
                }
            }
            else if (path is "active")
            {
                if (op == "remove")
                {
                    user.IsDeleted = false;
                }
                else
                {
                    user.IsDeleted = !ExtractBoolean(operation.Value, fallback: true);
                }
            }
            else if (path.StartsWith("emails", StringComparison.Ordinal))
            {
                ApplyEmailPatch(user, op, path, operation.Value);
            }
            else if (path.StartsWith("phonenumbers", StringComparison.Ordinal))
            {
                ApplyPhonePatch(user, op, path, operation.Value);
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
        var andParts = normalized.Split(" and ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (andParts.Length > 1)
        {
            foreach (var andPart in andParts)
            {
                query = ApplyFilter(query, andPart);
            }

            return query;
        }

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

        if (normalized.StartsWith("displayName eq ", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractFilterValue(normalized["displayName eq ".Length..]);
            return query.Where(x => x.DisplayName == value);
        }

        if (normalized.StartsWith("active eq ", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractFilterValue(normalized["active eq ".Length..]);
            var active = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            return active ? query.Where(x => !x.IsDeleted) : query.Where(x => x.IsDeleted);
        }

        if (normalized.StartsWith("emails.value eq ", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractFilterValue(normalized["emails.value eq ".Length..]);
            return query.Where(x => x.Email == value);
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
        if (first.ValueKind == JsonValueKind.String)
        {
            return first.GetString();
        }

        return first.TryGetProperty("value", out var property) ? property.GetString() : null;
    }

    private static void ApplyPatchObject(NetIamIdentityUser user, string op, JsonElement value)
    {
        if (op == "remove")
        {
            return;
        }

        if (value.TryGetProperty("displayName", out var displayName))
        {
            user.DisplayName = ExtractScalarString(displayName) ?? user.DisplayName;
        }

        if (value.TryGetProperty("userName", out var userName))
        {
            user.UserName = ExtractScalarString(userName) ?? user.UserName;
        }

        if (value.TryGetProperty("active", out var active))
        {
            user.IsDeleted = !ExtractBoolean(active, fallback: true);
        }

        if (value.TryGetProperty("emails", out var emails))
        {
            ApplyEmailPatch(user, op, "emails", emails);
        }

        if (value.TryGetProperty("phoneNumbers", out var phoneNumbers))
        {
            ApplyPhonePatch(user, op, "phonenumbers", phoneNumbers);
        }

        if (value.TryGetProperty("name", out var nameNode)
            && nameNode.ValueKind == JsonValueKind.Object
            && nameNode.TryGetProperty("formatted", out var formatted))
        {
            user.DisplayName = ExtractScalarString(formatted) ?? user.DisplayName;
        }
    }

    private static void ApplyEmailPatch(NetIamIdentityUser user, string op, string path, JsonElement value)
    {
        if (op == "remove")
        {
            if (!ShouldApplyFilteredRemove(path, user.Email))
            {
                return;
            }

            user.Email = null;
            return;
        }

        var email = ExtractEmailValue(value);
        if (!string.IsNullOrWhiteSpace(email))
        {
            user.Email = email;
        }
    }

    private static void ApplyPhonePatch(NetIamIdentityUser user, string op, string path, JsonElement value)
    {
        if (op == "remove")
        {
            if (!ShouldApplyFilteredRemove(path, user.PhoneNumber))
            {
                return;
            }

            user.PhoneNumber = null;
            return;
        }

        var phone = ExtractPhoneValue(value);
        if (!string.IsNullOrWhiteSpace(phone))
        {
            user.PhoneNumber = phone;
        }
    }

    private static string? ExtractEmailValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("value", out var directValue))
            {
                return directValue.GetString();
            }

            return null;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return ExtractFirstArrayValue(value);
        }

        return null;
    }

    private static string? ExtractPhoneValue(JsonElement value)
    {
        return ExtractEmailValue(value);
    }

    private static string? ExtractScalarString(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static bool ExtractBoolean(JsonElement value, bool fallback)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => fallback
        };
    }

    private static bool ShouldApplyFilteredRemove(string path, string? currentValue)
    {
        var normalized = path.Trim().ToLowerInvariant();
        if (!normalized.Contains("[", StringComparison.Ordinal))
        {
            return true;
        }

        var start = normalized.IndexOf('[', StringComparison.Ordinal);
        var end = normalized.IndexOf(']', StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            return true;
        }

        var condition = normalized.Substring(start + 1, end - start - 1).Trim();
        var tokens = condition.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != 3 || !string.Equals(tokens[1], "eq", StringComparison.Ordinal))
        {
            return true;
        }

        var value = tokens[2].Trim('"');
        if (string.Equals(tokens[0], "value", StringComparison.Ordinal))
        {
            return string.Equals(currentValue, value, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(tokens[0], "type", StringComparison.Ordinal))
        {
            return string.Equals(value, "work", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }
}
