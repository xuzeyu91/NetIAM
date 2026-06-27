using Microsoft.Extensions.DependencyInjection;
using NetIAM.Integrations.Abstractions;
using NetIAM.Integrations.Providers;
using NetIAM.Integrations.Services;

namespace NetIAM.Integrations.Extensions;

public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddNetIamIntegrations(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddHttpClient<DingTalkAuthProviderHandler>();
        services.AddHttpClient<FeishuAuthProviderHandler>();
        services.AddHttpClient<WeComAuthProviderHandler>();

        services.AddScoped<IExternalAuthProviderHandler, DingTalkAuthProviderHandler>();
        services.AddScoped<IExternalAuthProviderHandler, FeishuAuthProviderHandler>();
        services.AddScoped<IExternalAuthProviderHandler, WeComAuthProviderHandler>();
        services.AddScoped<IExternalAuthProviderFactory, ExternalAuthProviderFactory>();

        services.AddScoped<IDirectorySyncProvider, DingTalkDirectorySyncProvider>();
        services.AddScoped<IDirectorySyncProvider, FeishuDirectorySyncProvider>();
        services.AddScoped<IDirectorySyncProvider, WeComDirectorySyncProvider>();
        services.AddScoped<IDirectorySyncProviderFactory, DirectorySyncProviderFactory>();

        return services;
    }
}
