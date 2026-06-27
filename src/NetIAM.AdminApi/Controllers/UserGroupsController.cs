using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Entities;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

namespace NetIAM.AdminApi.Controllers;

[ApiController]
[Route("api/admin/user-groups")]
public sealed class UserGroupsController(
    NetIamDbContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    IAuditService auditService) : ControllerBase
{
    public sealed record CreateUserGroupRequest(string Name, string? Description);
    public sealed record UpdateUserGroupRequest(string Name, string? Description);
    public sealed record UpdateUserGroupMembersRequest(IReadOnlyCollection<string>? UserIds);

    [HttpGet]
    [RequirePermission("group.read")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var groups = await dbContext.UserGroups
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var groupIds = groups.Select(x => x.Id).ToList();
        var memberCounts = groupIds.Count == 0
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : await dbContext.UserGroupMembers
                .Where(x => x.TenantId == tenantId && groupIds.Contains(x.UserGroupId) && !x.IsDeleted)
                .GroupBy(x => x.UserGroupId)
                .Select(x => new { GroupId = x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancellationToken);

        return Ok(groups.Select(x => new
        {
            x.Id,
            x.Name,
            x.Description,
            memberCount = memberCounts.GetValueOrDefault(x.Id, 0),
            x.CreateTime,
            x.UpdateTime
        }));
    }

    [HttpGet("{id}/members")]
    [RequirePermission("group.read")]
    public async Task<IActionResult> ListMembers(string id, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var group = await dbContext.UserGroups
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        var memberUserIds = await dbContext.UserGroupMembers
            .Where(x => x.TenantId == tenantId && x.UserGroupId == id && !x.IsDeleted)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        if (memberUserIds.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var members = await dbContext.Users
            .Where(x => x.TenantId == tenantId && memberUserIds.Contains(x.Id) && !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                x.UserName,
                x.DisplayName,
                x.Email,
                x.PhoneNumber
            })
            .OrderBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        return Ok(members);
    }

    [HttpPost]
    [RequirePermission("group.write")]
    public async Task<IActionResult> Create([FromBody] CreateUserGroupRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        var nameExists = await dbContext.UserGroups.AnyAsync(
            x => x.TenantId == tenantId && x.Name == request.Name && !x.IsDeleted,
            cancellationToken);
        if (nameExists)
        {
            return Conflict($"User group name already exists: {request.Name}.");
        }

        var group = new UserGroupEntity
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };
        dbContext.UserGroups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.user-group.created",
                $"User group {group.Name} created.",
                TargetJson: $$"""{"groupId":"{{group.Id}}"}"""),
            cancellationToken);

        return Ok(new
        {
            group.Id,
            group.Name,
            group.Description,
            memberCount = 0,
            group.CreateTime,
            group.UpdateTime
        });
    }

    [HttpPut("{id}")]
    [RequirePermission("group.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUserGroupRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var group = await dbContext.UserGroups
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required.");
        }

        var nameExists = await dbContext.UserGroups.AnyAsync(
            x => x.TenantId == tenantId && x.Id != id && x.Name == request.Name && !x.IsDeleted,
            cancellationToken);
        if (nameExists)
        {
            return Conflict($"User group name already exists: {request.Name}.");
        }

        group.Name = request.Name.Trim();
        group.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        group.UpdateTime = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var memberCount = await dbContext.UserGroupMembers
            .CountAsync(x => x.TenantId == tenantId && x.UserGroupId == id && !x.IsDeleted, cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.user-group.updated",
                $"User group {group.Name} updated.",
                TargetJson: $$"""{"groupId":"{{group.Id}}"}"""),
            cancellationToken);

        return Ok(new
        {
            group.Id,
            group.Name,
            group.Description,
            memberCount,
            group.CreateTime,
            group.UpdateTime
        });
    }

    [HttpPut("{id}/members")]
    [RequirePermission("group.write")]
    public async Task<IActionResult> ReplaceMembers(
        string id,
        [FromBody] UpdateUserGroupMembersRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var group = await dbContext.UserGroups
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        var requestedUserIds = (request.UserIds ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var validUserIds = requestedUserIds.Count == 0
            ? new List<string>()
            : await dbContext.Users
                .Where(x => x.TenantId == tenantId && requestedUserIds.Contains(x.Id) && !x.IsDeleted)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

        var existingMembers = await dbContext.UserGroupMembers
            .Where(x => x.TenantId == tenantId && x.UserGroupId == id && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var existingMember in existingMembers)
        {
            existingMember.IsDeleted = true;
            existingMember.UpdateTime = DateTimeOffset.UtcNow;
        }

        foreach (var validUserId in validUserIds)
        {
            dbContext.UserGroupMembers.Add(new UserGroupMemberEntity
            {
                TenantId = tenantId,
                UserGroupId = id,
                UserId = validUserId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.user-group.members.replaced",
                $"User group {group.Name} members replaced.",
                TargetJson: $$"""{"groupId":"{{group.Id}}","memberCount":{{validUserIds.Count}}}"""),
            cancellationToken);

        return Ok(new
        {
            groupId = group.Id,
            memberCount = validUserIds.Count,
            userIds = validUserIds
        });
    }

    [HttpDelete("{id}")]
    [RequirePermission("group.write")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var tenantId = tenantContextAccessor.GetTenantId();
        var group = await dbContext.UserGroups
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id && !x.IsDeleted, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        group.IsDeleted = true;
        group.UpdateTime = DateTimeOffset.UtcNow;

        var members = await dbContext.UserGroupMembers
            .Where(x => x.TenantId == tenantId && x.UserGroupId == id && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var member in members)
        {
            member.IsDeleted = true;
            member.UpdateTime = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "admin.user-group.deleted",
                $"User group {group.Name} deleted.",
                TargetJson: $$"""{"groupId":"{{group.Id}}"}"""),
            cancellationToken);

        return NoContent();
    }
}
