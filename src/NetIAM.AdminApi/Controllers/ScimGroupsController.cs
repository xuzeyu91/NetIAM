using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.AdminApi.Scim;
using NetIAM.Domain.Entities;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Persistence;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[ScimTokenAuthorize]
[Route("scim/v2/Groups")]
public sealed class ScimGroupsController(NetIamDbContext dbContext) : ControllerBase
{
    public sealed record ScimMemberRef(string Value);
    public sealed record ScimGroupRequest(string DisplayName, IReadOnlyCollection<ScimMemberRef>? Members);
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

        var query = dbContext.UserGroups.Where(x => x.TenantId == tenantId && !x.IsDeleted);
        query = ApplyFilter(query, filter);
        var totalResults = await query.CountAsync(cancellationToken);
        var groups = await query
            .OrderBy(x => x.Name)
            .Skip(normalizedStart - 1)
            .Take(normalizedCount)
            .ToListAsync(cancellationToken);

        var groupIds = groups.Select(x => x.Id).ToList();
        var members = await dbContext.UserGroupMembers
            .Where(x => x.TenantId == tenantId && groupIds.Contains(x.UserGroupId) && !x.IsDeleted)
            .GroupBy(x => x.UserGroupId)
            .ToDictionaryAsync(x => x.Key, x => (IReadOnlyCollection<string>)x.Select(y => y.UserId).ToArray(), cancellationToken);

        var resources = groups
            .Select(group => ScimResponseFactory.BuildGroup(group, members.GetValueOrDefault(group.Id, Array.Empty<string>())))
            .ToArray();

        return Ok(ScimResponseFactory.BuildListResponse(resources, totalResults, normalizedStart, resources.Length));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var group = await dbContext.UserGroups.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        var members = await dbContext.UserGroupMembers
            .Where(x => x.TenantId == tenantId && x.UserGroupId == id && !x.IsDeleted)
            .Select(x => x.UserId)
            .ToArrayAsync(cancellationToken);

        return Ok(ScimResponseFactory.BuildGroup(group, members));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ScimGroupRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var group = new UserGroupEntity
        {
            TenantId = tenantId,
            Name = request.DisplayName
        };
        dbContext.UserGroups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);

        await ReplaceMembersAsync(tenantId, group.Id, request.Members, cancellationToken);
        return Created($"/scim/v2/Groups/{group.Id}", await BuildGroupResponseAsync(tenantId, group.Id, cancellationToken));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Replace(string id, [FromBody] ScimGroupRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var group = await dbContext.UserGroups.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        group.Name = request.DisplayName;
        group.UpdateTime = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await ReplaceMembersAsync(tenantId, group.Id, request.Members, cancellationToken);
        return Ok(await BuildGroupResponseAsync(tenantId, group.Id, cancellationToken));
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Patch(string id, [FromBody] ScimPatchRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var group = await dbContext.UserGroups.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);
        if (group is null)
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
                ApplyPatchObject(group, op, operation.Value);

                if (operation.Value.TryGetProperty("members", out var objectMembers))
                {
                    var memberIds = ExtractMemberIds(objectMembers);
                    if (op == "replace")
                    {
                        await ReplaceMembersAsync(tenantId, group.Id, memberIds.Select(x => new ScimMemberRef(x)).ToArray(), cancellationToken);
                    }
                    else if (op == "add")
                    {
                        await AddMembersAsync(tenantId, group.Id, memberIds, cancellationToken);
                    }
                    else
                    {
                        await RemoveMembersAsync(tenantId, group.Id, memberIds, removeAllWhenEmpty: memberIds.Count == 0, cancellationToken);
                    }
                }

                continue;
            }

            if (path is "displayname")
            {
                if (op != "remove")
                {
                    group.Name = operation.Value.GetString() ?? group.Name;
                }
            }
            else if (path.StartsWith("members", StringComparison.Ordinal))
            {
                if (op == "replace")
                {
                    var members = ExtractMemberIds(operation.Value)
                        .Select(x => new ScimMemberRef(x))
                        .ToArray();
                    await ReplaceMembersAsync(tenantId, group.Id, members, cancellationToken);
                }
                else if (op == "add")
                {
                    await AddMembersAsync(tenantId, group.Id, ExtractMemberIds(operation.Value), cancellationToken);
                }
                else
                {
                    var filterMember = ExtractMemberIdFromPathFilter(path);
                    var members = filterMember is not null
                        ? new[] { filterMember }
                        : ExtractMemberIds(operation.Value).ToArray();
                    await RemoveMembersAsync(tenantId, group.Id, members, removeAllWhenEmpty: filterMember is null && members.Length == 0, cancellationToken);
                }
            }
        }

        group.UpdateTime = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await BuildGroupResponseAsync(tenantId, group.Id, cancellationToken));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var tenantId = ResolveTenantId();
        var group = await dbContext.UserGroups.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        group.IsDeleted = true;
        group.UpdateTime = DateTimeOffset.UtcNow;

        var members = await dbContext.UserGroupMembers
            .Where(x => x.TenantId == tenantId && x.UserGroupId == group.Id && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var member in members)
        {
            member.IsDeleted = true;
            member.UpdateTime = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task ReplaceMembersAsync(
        string tenantId,
        string groupId,
        IReadOnlyCollection<ScimMemberRef>? members,
        CancellationToken cancellationToken)
    {
        await RemoveMembersAsync(tenantId, groupId, Array.Empty<string>(), removeAllWhenEmpty: true, cancellationToken);
        if (members is null || members.Count == 0)
        {
            return;
        }

        await AddMembersAsync(tenantId, groupId, members.Select(x => x.Value), cancellationToken);
    }

    private async Task AddMembersAsync(
        string tenantId,
        string groupId,
        IEnumerable<string> memberIds,
        CancellationToken cancellationToken)
    {
        var normalizedMemberIds = memberIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedMemberIds.Length == 0)
        {
            return;
        }

        var existingActiveMemberIds = await dbContext.UserGroupMembers
            .Where(x => x.TenantId == tenantId && x.UserGroupId == groupId && !x.IsDeleted)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        foreach (var memberId in normalizedMemberIds)
        {
            if (existingActiveMemberIds.Contains(memberId, StringComparer.Ordinal))
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

    private async Task RemoveMembersAsync(
        string tenantId,
        string groupId,
        IEnumerable<string> memberIds,
        bool removeAllWhenEmpty,
        CancellationToken cancellationToken)
    {
        var normalizedMemberIds = memberIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var query = dbContext.UserGroupMembers
            .Where(x => x.TenantId == tenantId && x.UserGroupId == groupId && !x.IsDeleted);
        if (normalizedMemberIds.Length > 0)
        {
            query = query.Where(x => normalizedMemberIds.Contains(x.UserId));
        }
        else if (!removeAllWhenEmpty)
        {
            return;
        }

        var existingMembers = await query.ToListAsync(cancellationToken);
        foreach (var existing in existingMembers)
        {
            existing.IsDeleted = true;
            existing.UpdateTime = DateTimeOffset.UtcNow;
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

    private static IQueryable<UserGroupEntity> ApplyFilter(IQueryable<UserGroupEntity> query, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return query;
        }

        var normalized = filter.Trim();
        if (normalized.StartsWith("displayName eq ", StringComparison.OrdinalIgnoreCase))
        {
            var value = normalized["displayName eq ".Length..].Trim().Trim('"');
            return query.Where(x => x.Name == value);
        }

        if (normalized.StartsWith("id eq ", StringComparison.OrdinalIgnoreCase))
        {
            var value = normalized["id eq ".Length..].Trim().Trim('"');
            return query.Where(x => x.Id == value);
        }

        return query;
    }

    private static void ApplyPatchObject(UserGroupEntity group, string op, JsonElement value)
    {
        if (op == "remove")
        {
            return;
        }

        if (value.TryGetProperty("displayName", out var displayName))
        {
            group.Name = displayName.GetString() ?? group.Name;
        }
    }

    private static IReadOnlyCollection<string> ExtractMemberIds(JsonElement value)
    {
        var memberIds = new List<string>();
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in value.EnumerateArray())
            {
                var memberId = node.ValueKind == JsonValueKind.String
                    ? node.GetString()
                    : node.TryGetProperty("value", out var memberNode) ? memberNode.GetString() : null;
                if (!string.IsNullOrWhiteSpace(memberId))
                {
                    memberIds.Add(memberId.Trim());
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("value", out var singleMember))
        {
            var memberId = singleMember.GetString();
            if (!string.IsNullOrWhiteSpace(memberId))
            {
                memberIds.Add(memberId.Trim());
            }
        }
        else if (value.ValueKind == JsonValueKind.String)
        {
            var memberId = value.GetString();
            if (!string.IsNullOrWhiteSpace(memberId))
            {
                memberIds.Add(memberId.Trim());
            }
        }

        return memberIds.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string? ExtractMemberIdFromPathFilter(string path)
    {
        var normalized = path.Trim().ToLowerInvariant();
        var start = normalized.IndexOf('[', StringComparison.Ordinal);
        var end = normalized.IndexOf(']', StringComparison.Ordinal);
        if (start < 0 || end <= start)
        {
            return null;
        }

        var condition = normalized.Substring(start + 1, end - start - 1).Trim();
        var tokens = condition.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 3
            && string.Equals(tokens[0], "value", StringComparison.Ordinal)
            && string.Equals(tokens[1], "eq", StringComparison.Ordinal))
        {
            return tokens[2].Trim('"');
        }

        return null;
    }
}
