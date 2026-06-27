using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.AdminApi.Scim;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Identity;
using NetIAM.Infrastructure.Persistence;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[ScimTokenAuthorize]
[Route("scim/v2/Bulk")]
public sealed class ScimBulkController(
    UserManager<NetIamIdentityUser> userManager,
    NetIamDbContext dbContext) : ControllerBase
{
    public sealed record ScimBulkRequest(
        IReadOnlyCollection<string>? Schemas,
        IReadOnlyCollection<ScimBulkOperation>? Operations,
        int? FailOnErrors = null);

    public sealed record ScimBulkOperation(
        string Method,
        string? Path,
        string? BulkId,
        JsonElement? Data);

    [HttpPost]
    public async Task<IActionResult> Execute([FromBody] ScimBulkRequest request, CancellationToken cancellationToken)
    {
        var operations = request.Operations ?? Array.Empty<ScimBulkOperation>();
        var failOnErrors = request.FailOnErrors.GetValueOrDefault(int.MaxValue);
        var tenantId = ResolveTenantId();
        var responses = new List<object>(operations.Count);
        var errorCount = 0;

        foreach (var operation in operations)
        {
            var result = await ExecuteOperationAsync(tenantId, operation, cancellationToken);
            responses.Add(result.ResponseBody);
            if (!result.Success)
            {
                errorCount++;
            }

            if (errorCount >= failOnErrors)
            {
                break;
            }
        }

        return Ok(new
        {
            schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:BulkResponse" },
            Operations = responses
        });
    }

    private async Task<(bool Success, object ResponseBody)> ExecuteOperationAsync(
        string tenantId,
        ScimBulkOperation operation,
        CancellationToken cancellationToken)
    {
        var method = operation.Method?.Trim().ToUpperInvariant() ?? string.Empty;
        var path = NormalizePath(operation.Path);
        if (string.IsNullOrWhiteSpace(method) || string.IsNullOrWhiteSpace(path))
        {
            return (false, BuildError(operation, 400, "Invalid method or path."));
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return (false, BuildError(operation, 400, "Invalid path."));
        }

        try
        {
            if (string.Equals(segments[0], "Users", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteUserOperationAsync(tenantId, operation, method, segments, cancellationToken);
            }

            if (string.Equals(segments[0], "Groups", StringComparison.OrdinalIgnoreCase))
            {
                return await ExecuteGroupOperationAsync(tenantId, operation, method, segments, cancellationToken);
            }

            return (false, BuildError(operation, 400, $"Unsupported SCIM bulk path: {path}."));
        }
        catch (Exception ex)
        {
            return (false, BuildError(operation, 400, ex.Message));
        }
    }

    private async Task<(bool Success, object ResponseBody)> ExecuteUserOperationAsync(
        string tenantId,
        ScimBulkOperation operation,
        string method,
        IReadOnlyList<string> segments,
        CancellationToken cancellationToken)
    {
        if (method == "POST" && segments.Count == 1)
        {
            if (!operation.Data.HasValue || operation.Data.Value.ValueKind != JsonValueKind.Object)
            {
                return (false, BuildError(operation, 400, "Data payload is required for user creation."));
            }

            var payload = operation.Data.Value;
            var userName = payload.TryGetProperty("userName", out var userNameNode)
                ? userNameNode.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(userName))
            {
                return (false, BuildError(operation, 400, "userName is required."));
            }

            var exists = await userManager.Users.AnyAsync(x => x.TenantId == tenantId && x.UserName == userName && !x.IsDeleted, cancellationToken);
            if (exists)
            {
                return (false, BuildError(operation, 409, $"User already exists: {userName}."));
            }

            var createdUser = new NetIamIdentityUser
            {
                TenantId = tenantId,
                UserName = userName,
                DisplayName = payload.TryGetProperty("displayName", out var displayNameNode) ? displayNameNode.GetString() ?? userName : userName,
                Email = ExtractScalarValue(payload, "emails"),
                PhoneNumber = ExtractScalarValue(payload, "phoneNumbers"),
                IsDeleted = payload.TryGetProperty("active", out var activeNode) && activeNode.ValueKind == JsonValueKind.False,
                DataOrigin = DataOriginType.Local
            };

            var password = $"Scim#{Guid.NewGuid():N}A1!";
            var createResult = await userManager.CreateAsync(createdUser, password);
            if (!createResult.Succeeded)
            {
                return (false, BuildError(operation, 400, string.Join("; ", createResult.Errors.Select(x => x.Description))));
            }

            return (true, BuildSuccess(operation, 201, $"/scim/v2/Users/{createdUser.Id}", ScimResponseFactory.BuildUser(createdUser)));
        }

        if (segments.Count != 2)
        {
            return (false, BuildError(operation, 400, "User operation path must be /Users or /Users/{id}."));
        }

        var userId = segments[1];
        var user = await userManager.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, BuildError(operation, 404, $"User not found: {userId}."));
        }

        if (method == "DELETE")
        {
            user.IsDeleted = true;
            user.UpdateTime = DateTimeOffset.UtcNow;
            await userManager.UpdateAsync(user);
            return (true, BuildSuccess(operation, 204, $"/scim/v2/Users/{user.Id}", null));
        }

        if (!operation.Data.HasValue || operation.Data.Value.ValueKind != JsonValueKind.Object)
        {
            return (false, BuildError(operation, 400, "Data payload is required."));
        }

        var data = operation.Data.Value;
        if (method == "PUT")
        {
            var userName = data.TryGetProperty("userName", out var userNameNode) ? userNameNode.GetString() : user.UserName;
            user.UserName = userName ?? user.UserName;
            user.DisplayName = data.TryGetProperty("displayName", out var displayNameNode) ? displayNameNode.GetString() ?? user.DisplayName : user.DisplayName;
            user.Email = ExtractScalarValue(data, "emails");
            user.PhoneNumber = ExtractScalarValue(data, "phoneNumbers");
            if (data.TryGetProperty("active", out var activeNode))
            {
                user.IsDeleted = activeNode.ValueKind == JsonValueKind.False;
            }

            user.UpdateTime = DateTimeOffset.UtcNow;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return (false, BuildError(operation, 400, string.Join("; ", updateResult.Errors.Select(x => x.Description))));
            }

            return (true, BuildSuccess(operation, 200, $"/scim/v2/Users/{user.Id}", ScimResponseFactory.BuildUser(user)));
        }

        if (method == "PATCH")
        {
            ApplyUserPatch(user, data);
            user.UpdateTime = DateTimeOffset.UtcNow;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return (false, BuildError(operation, 400, string.Join("; ", updateResult.Errors.Select(x => x.Description))));
            }

            return (true, BuildSuccess(operation, 200, $"/scim/v2/Users/{user.Id}", ScimResponseFactory.BuildUser(user)));
        }

        return (false, BuildError(operation, 400, $"Unsupported user operation method: {method}."));
    }

    private async Task<(bool Success, object ResponseBody)> ExecuteGroupOperationAsync(
        string tenantId,
        ScimBulkOperation operation,
        string method,
        IReadOnlyList<string> segments,
        CancellationToken cancellationToken)
    {
        if (method == "POST" && segments.Count == 1)
        {
            if (!operation.Data.HasValue || operation.Data.Value.ValueKind != JsonValueKind.Object)
            {
                return (false, BuildError(operation, 400, "Data payload is required for group creation."));
            }

            var payload = operation.Data.Value;
            var displayName = payload.TryGetProperty("displayName", out var displayNameNode)
                ? displayNameNode.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return (false, BuildError(operation, 400, "displayName is required."));
            }

            var createdGroup = new UserGroupEntity
            {
                TenantId = tenantId,
                Name = displayName!
            };
            dbContext.UserGroups.Add(createdGroup);
            await dbContext.SaveChangesAsync(cancellationToken);
            await ReplaceGroupMembersAsync(tenantId, createdGroup.Id, ExtractMemberIds(payload), cancellationToken);

            var response = await BuildGroupResponseAsync(tenantId, createdGroup.Id, cancellationToken);
            return (true, BuildSuccess(operation, 201, $"/scim/v2/Groups/{createdGroup.Id}", response));
        }

        if (segments.Count != 2)
        {
            return (false, BuildError(operation, 400, "Group operation path must be /Groups or /Groups/{id}."));
        }

        var groupId = segments[1];
        var group = await dbContext.UserGroups.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Id == groupId && !x.IsDeleted,
            cancellationToken);
        if (group is null)
        {
            return (false, BuildError(operation, 404, $"Group not found: {groupId}."));
        }

        if (method == "DELETE")
        {
            group.IsDeleted = true;
            group.UpdateTime = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await RemoveGroupMembersAsync(tenantId, group.Id, Array.Empty<string>(), removeAllWhenEmpty: true, cancellationToken);
            return (true, BuildSuccess(operation, 204, $"/scim/v2/Groups/{group.Id}", null));
        }

        if (!operation.Data.HasValue || operation.Data.Value.ValueKind != JsonValueKind.Object)
        {
            return (false, BuildError(operation, 400, "Data payload is required."));
        }

        var data = operation.Data.Value;
        if (method == "PUT")
        {
            if (data.TryGetProperty("displayName", out var displayNameNode))
            {
                group.Name = displayNameNode.GetString() ?? group.Name;
            }

            group.UpdateTime = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await ReplaceGroupMembersAsync(tenantId, group.Id, ExtractMemberIds(data), cancellationToken);
            var response = await BuildGroupResponseAsync(tenantId, group.Id, cancellationToken);
            return (true, BuildSuccess(operation, 200, $"/scim/v2/Groups/{group.Id}", response));
        }

        if (method == "PATCH")
        {
            await ApplyGroupPatchAsync(tenantId, group, data, cancellationToken);
            group.UpdateTime = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            var response = await BuildGroupResponseAsync(tenantId, group.Id, cancellationToken);
            return (true, BuildSuccess(operation, 200, $"/scim/v2/Groups/{group.Id}", response));
        }

        return (false, BuildError(operation, 400, $"Unsupported group operation method: {method}."));
    }

    private static object BuildSuccess(ScimBulkOperation operation, int status, string? location, object? response)
    {
        return new
        {
            operation.BulkId,
            operation.Method,
            operation.Path,
            status = status.ToString(),
            location,
            response
        };
    }

    private static object BuildError(ScimBulkOperation operation, int status, string detail)
    {
        return new
        {
            operation.BulkId,
            operation.Method,
            operation.Path,
            status = status.ToString(),
            response = new { detail }
        };
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Trim();
        if (normalized.StartsWith('/'))
        {
            normalized = normalized[1..];
        }

        var queryIndex = normalized.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex >= 0)
        {
            normalized = normalized[..queryIndex];
        }

        return normalized;
    }

    private static string? ExtractScalarValue(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        if (property.ValueKind == JsonValueKind.Array)
        {
            var first = property.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.String)
            {
                return first.GetString();
            }

            if (first.TryGetProperty("value", out var value))
            {
                return value.GetString();
            }
        }

        if (property.ValueKind == JsonValueKind.Object && property.TryGetProperty("value", out var objectValue))
        {
            return objectValue.GetString();
        }

        return null;
    }

    private static void ApplyUserPatch(NetIamIdentityUser user, JsonElement patchPayload)
    {
        if (!patchPayload.TryGetProperty("Operations", out var operations) || operations.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var operation in operations.EnumerateArray())
        {
            var op = operation.TryGetProperty("op", out var opNode)
                ? (opNode.GetString() ?? string.Empty).Trim().ToLowerInvariant()
                : string.Empty;
            var path = operation.TryGetProperty("path", out var pathNode)
                ? (pathNode.GetString() ?? string.Empty).Trim().ToLowerInvariant()
                : string.Empty;
            var value = operation.TryGetProperty("value", out var valueNode) ? valueNode : default;

            if (string.IsNullOrWhiteSpace(op))
            {
                continue;
            }

            if (path is "displayname")
            {
                if (op == "remove")
                {
                    user.DisplayName = user.UserName ?? user.DisplayName;
                }
                else if (value.ValueKind == JsonValueKind.String)
                {
                    user.DisplayName = value.GetString() ?? user.DisplayName;
                }

                continue;
            }

            if (path is "username")
            {
                if (op != "remove" && value.ValueKind == JsonValueKind.String)
                {
                    user.UserName = value.GetString() ?? user.UserName;
                }

                continue;
            }

            if (path is "active")
            {
                if (op == "remove")
                {
                    user.IsDeleted = false;
                }
                else if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    user.IsDeleted = value.ValueKind == JsonValueKind.False;
                }
            }

            if (path.StartsWith("emails", StringComparison.Ordinal))
            {
                if (op == "remove")
                {
                    user.Email = null;
                }
                else
                {
                    user.Email = ExtractPatchValue(value) ?? user.Email;
                }
            }

            if (path.StartsWith("phonenumbers", StringComparison.Ordinal))
            {
                if (op == "remove")
                {
                    user.PhoneNumber = null;
                }
                else
                {
                    user.PhoneNumber = ExtractPatchValue(value) ?? user.PhoneNumber;
                }
            }
        }
    }

    private async Task ApplyGroupPatchAsync(
        string tenantId,
        UserGroupEntity group,
        JsonElement patchPayload,
        CancellationToken cancellationToken)
    {
        if (!patchPayload.TryGetProperty("Operations", out var operations) || operations.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var operation in operations.EnumerateArray())
        {
            var op = operation.TryGetProperty("op", out var opNode)
                ? (opNode.GetString() ?? string.Empty).Trim().ToLowerInvariant()
                : string.Empty;
            var path = operation.TryGetProperty("path", out var pathNode)
                ? (pathNode.GetString() ?? string.Empty).Trim().ToLowerInvariant()
                : string.Empty;
            var value = operation.TryGetProperty("value", out var valueNode) ? valueNode : default;

            if (string.IsNullOrWhiteSpace(op))
            {
                continue;
            }

            if (path is "displayname" && op != "remove" && value.ValueKind == JsonValueKind.String)
            {
                group.Name = value.GetString() ?? group.Name;
            }

            if (path.StartsWith("members", StringComparison.Ordinal))
            {
                var memberIds = ExtractMemberIds(value).ToArray();
                if (op == "replace")
                {
                    await ReplaceGroupMembersAsync(tenantId, group.Id, memberIds, cancellationToken);
                }
                else if (op == "add")
                {
                    await AddGroupMembersAsync(tenantId, group.Id, memberIds, cancellationToken);
                }
                else if (op == "remove")
                {
                    var filterMember = ExtractMemberIdFromPath(path);
                    var removeIds = filterMember is not null ? new[] { filterMember } : memberIds;
                    await RemoveGroupMembersAsync(
                        tenantId,
                        group.Id,
                        removeIds,
                        removeAllWhenEmpty: filterMember is null && removeIds.Length == 0,
                        cancellationToken);
                }
            }
        }
    }

    private static string? ExtractPatchValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            var first = value.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.String)
            {
                return first.GetString();
            }

            if (first.TryGetProperty("value", out var firstValue))
            {
                return firstValue.GetString();
            }
        }

        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("value", out var objectValue))
        {
            return objectValue.GetString();
        }

        return null;
    }

    private static IReadOnlyCollection<string> ExtractMemberIds(JsonElement payload)
    {
        if (!payload.TryGetProperty("members", out var members))
        {
            if (payload.ValueKind == JsonValueKind.Array)
            {
                members = payload;
            }
            else
            {
                return Array.Empty<string>();
            }
        }

        var result = new List<string>();
        if (members.ValueKind == JsonValueKind.Array)
        {
            foreach (var member in members.EnumerateArray())
            {
                var userId = member.ValueKind == JsonValueKind.String
                    ? member.GetString()
                    : member.TryGetProperty("value", out var valueNode) ? valueNode.GetString() : null;
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    result.Add(userId.Trim());
                }
            }
        }

        return result.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string? ExtractMemberIdFromPath(string path)
    {
        var normalized = path.Trim().ToLowerInvariant();
        var start = normalized.IndexOf('[', StringComparison.Ordinal);
        var end = normalized.IndexOf(']', StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            return null;
        }

        var condition = normalized.Substring(start + 1, end - start - 1).Trim();
        var parts = condition.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3
            && string.Equals(parts[0], "value", StringComparison.Ordinal)
            && string.Equals(parts[1], "eq", StringComparison.Ordinal))
        {
            return parts[2].Trim('"');
        }

        return null;
    }

    private async Task ReplaceGroupMembersAsync(
        string tenantId,
        string groupId,
        IReadOnlyCollection<string> memberIds,
        CancellationToken cancellationToken)
    {
        await RemoveGroupMembersAsync(tenantId, groupId, Array.Empty<string>(), removeAllWhenEmpty: true, cancellationToken);
        await AddGroupMembersAsync(tenantId, groupId, memberIds, cancellationToken);
    }

    private async Task AddGroupMembersAsync(
        string tenantId,
        string groupId,
        IReadOnlyCollection<string> memberIds,
        CancellationToken cancellationToken)
    {
        if (memberIds.Count == 0)
        {
            return;
        }

        var existingMemberIds = await dbContext.UserGroupMembers
            .Where(x => x.TenantId == tenantId && x.UserGroupId == groupId && !x.IsDeleted)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);
        foreach (var memberId in memberIds.Distinct(StringComparer.Ordinal))
        {
            if (existingMemberIds.Contains(memberId, StringComparer.Ordinal))
            {
                continue;
            }

            var userExists = await dbContext.Users.AnyAsync(
                x => x.TenantId == tenantId && x.Id == memberId && !x.IsDeleted,
                cancellationToken);
            if (!userExists)
            {
                continue;
            }

            dbContext.UserGroupMembers.Add(new UserGroupMemberEntity
            {
                TenantId = tenantId,
                UserGroupId = groupId,
                UserId = memberId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveGroupMembersAsync(
        string tenantId,
        string groupId,
        IReadOnlyCollection<string> memberIds,
        bool removeAllWhenEmpty,
        CancellationToken cancellationToken)
    {
        var query = dbContext.UserGroupMembers
            .Where(x => x.TenantId == tenantId && x.UserGroupId == groupId && !x.IsDeleted);
        if (memberIds.Count > 0)
        {
            query = query.Where(x => memberIds.Contains(x.UserId));
        }
        else if (!removeAllWhenEmpty)
        {
            return;
        }

        var targets = await query.ToListAsync(cancellationToken);
        foreach (var target in targets)
        {
            target.IsDeleted = true;
            target.UpdateTime = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<object> BuildGroupResponseAsync(string tenantId, string groupId, CancellationToken cancellationToken)
    {
        var group = await dbContext.UserGroups.FirstAsync(x => x.TenantId == tenantId && x.Id == groupId, cancellationToken);
        var members = await dbContext.UserGroupMembers
            .Where(x => x.TenantId == tenantId && x.UserGroupId == groupId && !x.IsDeleted)
            .Select(x => x.UserId)
            .ToArrayAsync(cancellationToken);
        return ScimResponseFactory.BuildGroup(group, members);
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
}
