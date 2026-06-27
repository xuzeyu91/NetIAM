using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Contracts;
using NetIAM.Domain.Entities;
using NetIAM.Infrastructure.Persistence;

namespace NetIAM.Infrastructure.Services;

public interface IScimTokenService
{
    Task<(ScimAccessTokenEntity Entity, string PlainToken)> CreateTokenAsync(
        string tenantId,
        string name,
        int expiresInDays,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ScimAccessTokenEntity>> ListTokensAsync(string tenantId, CancellationToken cancellationToken = default);

    Task<bool> RevokeTokenAsync(string tenantId, string tokenId, CancellationToken cancellationToken = default);

    Task<ScimPrincipalContext?> ValidateAsync(string plainToken, string? requestId = null, CancellationToken cancellationToken = default);
}

public sealed class ScimTokenService(NetIamDbContext dbContext) : IScimTokenService
{
    public async Task<(ScimAccessTokenEntity Entity, string PlainToken)> CreateTokenAsync(
        string tenantId,
        string name,
        int expiresInDays,
        CancellationToken cancellationToken = default)
    {
        var plainToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var tokenHash = HashToken(plainToken);

        var entity = new ScimAccessTokenEntity
        {
            TenantId = tenantId,
            Name = name,
            TokenHash = tokenHash,
            ExpiresTime = expiresInDays <= 0 ? null : DateTimeOffset.UtcNow.AddDays(expiresInDays),
            IsActive = true
        };
        dbContext.ScimAccessTokens.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (entity, plainToken);
    }

    public async Task<IReadOnlyCollection<ScimAccessTokenEntity>> ListTokensAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ScimAccessTokens
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .OrderByDescending(x => x.CreateTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeTokenAsync(string tenantId, string tokenId, CancellationToken cancellationToken = default)
    {
        var token = await dbContext.ScimAccessTokens
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == tokenId && !x.IsDeleted, cancellationToken);
        if (token is null)
        {
            return false;
        }

        token.IsActive = false;
        token.UpdateTime = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ScimPrincipalContext?> ValidateAsync(
        string plainToken,
        string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        var hash = HashToken(plainToken);
        var now = DateTimeOffset.UtcNow;

        var token = await dbContext.ScimAccessTokens
            .FirstOrDefaultAsync(
                x => x.TokenHash == hash
                     && x.IsActive
                     && !x.IsDeleted
                     && (x.ExpiresTime == null || x.ExpiresTime > now),
                cancellationToken);
        if (token is null)
        {
            return null;
        }

        token.LastUsedTime = now;
        token.UpdateTime = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ScimPrincipalContext(token.TenantId, token.Name, requestId);
    }

    private static string HashToken(string plainToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(bytes);
    }
}
