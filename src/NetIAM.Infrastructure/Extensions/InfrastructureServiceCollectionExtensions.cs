using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetIAM.Infrastructure.Authorization;
using NetIAM.Infrastructure.Identity;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

namespace NetIAM.Infrastructure.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddNetIamInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=netiam;Username=postgres;Password=postgres";

        services.AddDbContext<NetIamDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.UseOpenIddict();
        });

        services
            .AddIdentityCore<NetIamIdentityUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<NetIamDbContext>()
            .AddSignInManager<SignInManager<NetIamIdentityUser>>()
            .AddDefaultTokenProviders();

        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContextAccessor, HttpHeaderTenantContextAccessor>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAccountBindingService, AccountBindingService>();
        services.AddScoped<IDirectorySyncService, DirectorySyncService>();
        services.AddScoped<IRbacService, RbacService>();
        services.AddScoped<IScimTokenService, ScimTokenService>();
        services.AddScoped<ISamlService, SamlService>();
        services.AddScoped<IAuthorizationHandler, RbacPermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, RbacPermissionPolicyProvider>();
        services.AddScoped<INetIamDataSeeder, NetIamDataSeeder>();
        return services;
    }
}
