using NetIAM.Domain.Entities;
using NetIAM.Infrastructure.Identity;

namespace NetIAM.AdminApi.Scim;

internal static class ScimResponseFactory
{
    public const string UserSchema = "urn:ietf:params:scim:schemas:core:2.0:User";
    public const string GroupSchema = "urn:ietf:params:scim:schemas:core:2.0:Group";
    public const string ListResponseSchema = "urn:ietf:params:scim:api:messages:2.0:ListResponse";

    public static object BuildListResponse(IEnumerable<object> resources, int totalResults, int startIndex, int itemsPerPage)
    {
        return new
        {
            schemas = new[] { ListResponseSchema },
            totalResults,
            startIndex,
            itemsPerPage,
            Resources = resources
        };
    }

    public static object BuildUser(NetIamIdentityUser user)
    {
        return new
        {
            schemas = new[] { UserSchema },
            id = user.Id,
            userName = user.UserName,
            displayName = user.DisplayName,
            active = !user.IsDeleted,
            name = new
            {
                formatted = user.DisplayName
            },
            emails = string.IsNullOrWhiteSpace(user.Email)
                ? Array.Empty<object>()
                : new[]
                {
                    new
                    {
                        value = user.Email,
                        primary = true
                    }
                },
            phoneNumbers = string.IsNullOrWhiteSpace(user.PhoneNumber)
                ? Array.Empty<object>()
                : new[]
                {
                    new
                    {
                        value = user.PhoneNumber
                    }
                },
            meta = new
            {
                resourceType = "User",
                created = user.CreateTime,
                lastModified = user.UpdateTime
            }
        };
    }

    public static object BuildGroup(UserGroupEntity group, IReadOnlyCollection<string> memberUserIds)
    {
        return new
        {
            schemas = new[] { GroupSchema },
            id = group.Id,
            displayName = group.Name,
            members = memberUserIds.Select(x => new { value = x }).ToArray(),
            meta = new
            {
                resourceType = "Group",
                created = group.CreateTime,
                lastModified = group.UpdateTime
            }
        };
    }
}
