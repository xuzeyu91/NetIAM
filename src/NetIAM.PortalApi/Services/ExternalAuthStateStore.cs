using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace NetIAM.PortalApi.Services;

public interface IExternalAuthStateStore
{
    Task<string> CreateStateAsync(string tenantId, string providerCode, CancellationToken cancellationToken = default);

    Task<bool> ConsumeStateAsync(string tenantId, string providerCode, string state, CancellationToken cancellationToken = default);
}

public sealed class ExternalAuthStateStore(IDistributedCache distributedCache) : IExternalAuthStateStore
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromMinutes(5);

    public async Task<string> CreateStateAsync(string tenantId, string providerCode, CancellationToken cancellationToken = default)
    {
        var state = Guid.NewGuid().ToString("N");
        var cacheKey = BuildStateKey(tenantId, providerCode, state);
        await distributedCache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(new { createdAt = DateTimeOffset.UtcNow }),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = StateTtl },
            cancellationToken);
        return state;
    }

    public async Task<bool> ConsumeStateAsync(
        string tenantId,
        string providerCode,
        string state,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildStateKey(tenantId, providerCode, state);
        var value = await distributedCache.GetStringAsync(cacheKey, cancellationToken);
        if (value is null)
        {
            return false;
        }

        await distributedCache.RemoveAsync(cacheKey, cancellationToken);
        return true;
    }

    private static string BuildStateKey(string tenantId, string providerCode, string state)
    {
        return $"portal:auth-state:{tenantId}:{providerCode}:{state}";
    }
}
