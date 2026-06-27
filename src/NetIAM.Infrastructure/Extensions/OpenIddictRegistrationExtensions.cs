using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetIAM.Infrastructure.Persistence;
using OpenIddict.Abstractions;

namespace NetIAM.Infrastructure.Extensions;

public static class OpenIddictRegistrationExtensions
{
    public static IServiceCollection AddNetIamOpenIddictServer(this IServiceCollection services)
    {
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<NetIamDbContext>();
            })
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize")
                    .SetTokenEndpointUris("/connect/token")
                    .SetIntrospectionEndpointUris("/connect/introspect")
                    .SetUserInfoEndpointUris("/connect/userinfo");

                options.AllowAuthorizationCodeFlow()
                    .AllowClientCredentialsFlow()
                    .AllowRefreshTokenFlow();

                options.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Email,
                    "netiam.api");

                options.AddDevelopmentEncryptionCertificate()
                    .AddDevelopmentSigningCertificate();

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseAspNetCore();
            });

        return services;
    }

    public static IServiceCollection AddNetIamOpenIddictValidation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authority = configuration["AuthServer:Authority"] ?? "https://localhost:7001";

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<NetIamDbContext>();
            })
            .AddValidation(options =>
            {
                options.SetIssuer(authority);
                options.AddAudiences("netiam.api");
                options.UseAspNetCore();
            });

        return services;
    }
}
