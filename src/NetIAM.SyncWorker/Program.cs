using NetIAM.SyncWorker;
using NetIAM.Infrastructure.Extensions;
using NetIAM.Integrations.Extensions;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddNetIamInfrastructure(builder.Configuration);
builder.Services.AddNetIamIntegrations();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("NetIAM.SyncWorker"))
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();
    });

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();
builder.Services.AddSerilog();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
