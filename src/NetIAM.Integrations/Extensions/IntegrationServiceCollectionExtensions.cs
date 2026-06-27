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

        services.AddHttpClient<DingTalkAuthProviderHandler>(ConfigureProviderHttpClient);
        services.AddHttpClient<FeishuAuthProviderHandler>(ConfigureProviderHttpClient);
        services.AddHttpClient<WeComAuthProviderHandler>(ConfigureProviderHttpClient);
        services.AddHttpClient<DingTalkDirectorySyncProvider>(ConfigureProviderHttpClient);
        services.AddHttpClient<FeishuDirectorySyncProvider>(ConfigureProviderHttpClient);
        services.AddHttpClient<WeComDirectorySyncProvider>(ConfigureProviderHttpClient);

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

    private static void ConfigureProviderHttpClient(IServiceProvider _, HttpClient httpClient)
    {
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NetIAM-Integrations/1.0");
    }
}
