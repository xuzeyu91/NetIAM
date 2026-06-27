using NetIAM.Domain.Contracts;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;

namespace NetIAM.Integrations.Abstractions;

public interface IExternalAuthProviderHandler
{
    ExternalProviderType ProviderType { get; }

    Task<string> BuildAuthorizeUrlAsync(
        IdentityProviderEntity provider,
        ExternalAuthRequest request,
        CancellationToken cancellationToken = default);

    Task<ExternalUserAccessToken> ExchangeTokenAsync(
        IdentityProviderEntity provider,
        ExternalAuthCallback callback,
        CancellationToken cancellationToken = default);

    Task<ExternalUserProfile> GetUserProfileAsync(
        IdentityProviderEntity provider,
        ExternalUserAccessToken accessToken,
        CancellationToken cancellationToken = default);
}

public interface IExternalAuthProviderFactory
{
    IExternalAuthProviderHandler Resolve(ExternalProviderType providerType);
}

public interface IDirectorySyncProvider
{
    IdentitySourceProviderType ProviderType { get; }

    Task<IReadOnlyCollection<DirectoryOrganizationSnapshot>> PullOrganizationsAsync(
        IdentitySourceEntity identitySource,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DirectoryUserSnapshot>> PullUsersAsync(
        IdentitySourceEntity identitySource,
        CancellationToken cancellationToken = default);

    Task<DirectoryNormalizedEvent?> NormalizeWebhookAsync(
        IdentitySourceEntity identitySource,
        string payload,
        CancellationToken cancellationToken = default);
}

public interface IDirectorySyncProviderFactory
{
    IDirectorySyncProvider Resolve(IdentitySourceProviderType providerType);
}
