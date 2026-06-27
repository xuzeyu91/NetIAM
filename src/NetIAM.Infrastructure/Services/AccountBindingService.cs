using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Contracts;
using NetIAM.Domain.Entities;
using NetIAM.Infrastructure.Identity;
using NetIAM.Infrastructure.Persistence;

namespace NetIAM.Infrastructure.Services;

public enum AccountBindingStatus
{
    Bound = 1,
    AutoBound = 2,
    PendingBinding = 3
}

public sealed record AccountBindingResult(
    AccountBindingStatus Status,
    string ThirdPartyUserId,
    string? UserId);

public interface IAccountBindingService
{
    Task<AccountBindingResult> BindOrResolveAsync(
        string tenantId,
        string identityProviderId,
        ExternalUserProfile profile,
        CancellationToken cancellationToken = default);
}

public sealed class AccountBindingService(
    NetIamDbContext dbContext,
    UserManager<NetIamIdentityUser> userManager) : IAccountBindingService
{
    public async Task<AccountBindingResult> BindOrResolveAsync(
        string tenantId,
        string identityProviderId,
        ExternalUserProfile profile,
        CancellationToken cancellationToken = default)
    {
        var thirdParty = await dbContext.ThirdPartyUsers
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                     && x.IdentityProviderId == identityProviderId
                     && x.OpenId == profile.OpenId,
                cancellationToken);

        if (thirdParty is null)
        {
            thirdParty = new ThirdPartyUserEntity
            {
                TenantId = tenantId,
                IdentityProviderId = identityProviderId,
                OpenId = profile.OpenId,
                UnionId = profile.UnionId,
                Name = profile.Name,
                Email = profile.Email,
                Mobile = profile.Mobile,
                AvatarUrl = profile.AvatarUrl,
                RawProfileJson = profile.RawProfileJson,
                LastLoginTime = DateTimeOffset.UtcNow
            };
            dbContext.ThirdPartyUsers.Add(thirdParty);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            thirdParty.UnionId = profile.UnionId;
            thirdParty.Name = profile.Name;
            thirdParty.Email = profile.Email;
            thirdParty.Mobile = profile.Mobile;
            thirdParty.AvatarUrl = profile.AvatarUrl;
            thirdParty.RawProfileJson = profile.RawProfileJson;
            thirdParty.LastLoginTime = DateTimeOffset.UtcNow;
            thirdParty.UpdateTime = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var existingBind = await dbContext.UserIdpBinds
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.ThirdPartyUserId == thirdParty.Id && !x.IsDeleted,
                cancellationToken);

        if (existingBind is not null)
        {
            return new AccountBindingResult(AccountBindingStatus.Bound, thirdParty.Id, existingBind.UserId);
        }

        // eIAM style auto-bind: try matching user.external_id to platform openId.
        var matchedUser = await userManager.Users
            .Where(u => u.TenantId == tenantId && u.ExternalId == profile.OpenId && !u.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (matchedUser is null)
        {
            return new AccountBindingResult(AccountBindingStatus.PendingBinding, thirdParty.Id, null);
        }

        var bind = new UserIdpBindEntity
        {
            TenantId = tenantId,
            UserId = matchedUser.Id,
            ThirdPartyUserId = thirdParty.Id,
            BoundTime = DateTimeOffset.UtcNow
        };
        dbContext.UserIdpBinds.Add(bind);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AccountBindingResult(AccountBindingStatus.AutoBound, thirdParty.Id, matchedUser.Id);
    }
}
