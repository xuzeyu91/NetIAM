using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using NetIAM.Infrastructure.Extensions;
using NetIAM.Infrastructure.Services;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console();
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("NetIAM.AuthServer"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();
    });

builder.Services.AddNetIamInfrastructure(builder.Configuration);
builder.Services.AddNetIamOpenIddictServer();

var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = redisConnectionString; });
builder.Services
    .AddDataProtection()
    .SetApplicationName("NetIAM")
    .PersistKeysToStackExchangeRedis(
        ConnectionMultiplexer.Connect(redisConnectionString),
        "netiam:dataprotection:keys");

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth-fixed-window", configure =>
    {
        configure.PermitLimit = 100;
        configure.Window = TimeSpan.FromMinutes(1);
        configure.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        configure.QueueLimit = 50;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers()
    .RequireRateLimiting("auth-fixed-window");

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<INetIamDataSeeder>();
    await seeder.SeedAsync();
}

app.Run();
