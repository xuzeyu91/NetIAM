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
    public sealed record ScimPatchRequest(IReadOnlyCollection<ScimPatchOperation> Operations);
    public sealed record ScimPatchOperation(string Op, string? Path, JsonElement Value);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int startIndex = 1, [FromQuery] int count = 50, CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenantId();
        var normalizedStart = Math.Max(1, startIndex);
        var normalizedCount = Math.Clamp(count, 1, 200);

        var query = dbContext.UserGroups.Where(x => x.TenantId == tenantId && !x.IsDeleted);
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
                group.Name = operation.Value.GetString() ?? group.Name;
            }
            else if (path.StartsWith("members") && operation.Value.ValueKind == JsonValueKind.Array)
            {
                var members = operation.Value.EnumerateArray()
                    .Select(x => x.TryGetProperty("value", out var value) ? value.GetString() : null)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => new ScimMemberRef(x!))
                    .ToArray();
                await ReplaceMembersAsync(tenantId, group.Id, members, cancellationToken);
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
        var existingMembers = await dbContext.UserGroupMembers
            .Where(x => x.TenantId == tenantId && x.UserGroupId == groupId && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var existing in existingMembers)
        {
            existing.IsDeleted = true;
            existing.UpdateTime = DateTimeOffset.UtcNow;
        }

        if (members is not null)
        {
            foreach (var member in members)
            {
                var userExists = await dbContext.Users.AnyAsync(
                    x => x.TenantId == tenantId && x.Id == member.Value && !x.IsDeleted,
                    cancellationToken);
                if (!userExists)
                {
                    continue;
                }

                dbContext.UserGroupMembers.Add(new UserGroupMemberEntity
                {
                    TenantId = tenantId,
                    UserGroupId = groupId,
                    UserId = member.Value
                });
            }
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
