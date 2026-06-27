using NetIAM.Domain.Enums;
using NetIAM.Integrations.Abstractions;

namespace NetIAM.Integrations.Services;

public sealed class ExternalAuthProviderFactory(IEnumerable<IExternalAuthProviderHandler> providers) : IExternalAuthProviderFactory
{
    private readonly IReadOnlyDictionary<ExternalProviderType, IExternalAuthProviderHandler> _providerMap = providers.ToDictionary(x => x.ProviderType);

    public IExternalAuthProviderHandler Resolve(ExternalProviderType providerType)
    {
        if (_providerMap.TryGetValue(providerType, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"External provider not registered: {providerType}.");
    }
}

public sealed class DirectorySyncProviderFactory(IEnumerable<IDirectorySyncProvider> providers) : IDirectorySyncProviderFactory
{
    private readonly IReadOnlyDictionary<IdentitySourceProviderType, IDirectorySyncProvider> _providerMap = providers.ToDictionary(x => x.ProviderType);

    public IDirectorySyncProvider Resolve(IdentitySourceProviderType providerType)
    {
        if (_providerMap.TryGetValue(providerType, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"Directory sync provider not registered: {providerType}.");
    }
}
