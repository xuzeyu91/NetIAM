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
        services.AddHttpClient<DingTalkDirectorySyncProvider>();
        services.AddHttpClient<FeishuDirectorySyncProvider>();
        services.AddHttpClient<WeComDirectorySyncProvider>();

        services.AddScoped<IExternalAuthProviderHandler>(sp => sp.GetRequiredService<DingTalkAuthProviderHandler>());
        services.AddScoped<IExternalAuthProviderHandler>(sp => sp.GetRequiredService<FeishuAuthProviderHandler>());
        services.AddScoped<IExternalAuthProviderHandler>(sp => sp.GetRequiredService<WeComAuthProviderHandler>());
        services.AddScoped<IExternalAuthProviderFactory, ExternalAuthProviderFactory>();

        services.AddScoped<IDirectorySyncProvider>(sp => sp.GetRequiredService<DingTalkDirectorySyncProvider>());
        services.AddScoped<IDirectorySyncProvider>(sp => sp.GetRequiredService<FeishuDirectorySyncProvider>());
        services.AddScoped<IDirectorySyncProvider>(sp => sp.GetRequiredService<WeComDirectorySyncProvider>());
        services.AddScoped<IDirectorySyncProviderFactory, DirectorySyncProviderFactory>();

        return services;
    }
}
