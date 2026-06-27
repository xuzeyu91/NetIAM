namespace NetIAM.SyncWorker;

using Microsoft.EntityFrameworkCore;
using NetIAM.Infrastructure.Persistence;
using NetIAM.Infrastructure.Services;

public class Worker(
    ILogger<Worker> logger,
    IServiceProvider serviceProvider,
    IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var syncIntervalMinutes = configuration.GetValue("Sync:PullIntervalMinutes", 5);
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(syncIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<NetIamDbContext>();
                var directorySyncService = scope.ServiceProvider.GetRequiredService<IDirectorySyncService>();

                var enabledSources = await dbContext.IdentitySources
                    .Where(x => x.Enabled && !x.IsDeleted)
                    .Select(x => new { x.TenantId, x.Code })
                    .ToListAsync(stoppingToken);

                foreach (var source in enabledSources)
                {
                    await directorySyncService.RunFullSyncAsync(source.Code, source.TenantId, stoppingToken);
                }

                logger.LogInformation("Directory sync worker executed at {Time}. Sources: {Count}", DateTimeOffset.UtcNow, enabledSources.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Directory sync worker execution failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
