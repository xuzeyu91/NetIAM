using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Integrations.Abstractions;
using NetIAM.Infrastructure.Identity;
using NetIAM.Infrastructure.Persistence;

namespace NetIAM.Infrastructure.Services;

public sealed class DirectorySyncResult
{
    public int CreatedUsers { get; set; }
    public int UpdatedUsers { get; set; }
    public int CreatedOrganizations { get; set; }
    public int UpdatedOrganizations { get; set; }
}

public interface IDirectorySyncService
{
    Task<DirectorySyncResult> RunFullSyncAsync(string identitySourceCode, string tenantId, CancellationToken cancellationToken = default);

    Task<bool> HandleWebhookAsync(string identitySourceCode, string tenantId, string payload, CancellationToken cancellationToken = default);
}

public sealed class DirectorySyncService(
    NetIamDbContext dbContext,
    UserManager<NetIamIdentityUser> userManager,
    IDirectorySyncProviderFactory providerFactory,
    IAuditService auditService) : IDirectorySyncService
{
    public async Task<DirectorySyncResult> RunFullSyncAsync(
        string identitySourceCode,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var source = await dbContext.IdentitySources
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == identitySourceCode && !x.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException($"Identity source not found: {identitySourceCode}.");

        var provider = providerFactory.Resolve(source.ProviderType);
        var history = new IdentitySourceSyncHistoryEntity
        {
            TenantId = tenantId,
            IdentitySourceId = source.Id,
            TriggerMode = "pull",
            StartedTime = DateTimeOffset.UtcNow,
            Status = SyncStatus.Success
        };
        dbContext.IdentitySourceSyncHistories.Add(history);
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = new DirectorySyncResult();
        try
        {
            var organizations = await provider.PullOrganizationsAsync(source, cancellationToken);
            foreach (var snapshot in organizations)
            {
                var existing = await dbContext.Organizations
                    .FirstOrDefaultAsync(
                        x => x.TenantId == tenantId && x.IdentitySourceId == source.Id && x.ExternalId == snapshot.ExternalId,
                        cancellationToken);

                if (existing is null)
                {
                    var parent = snapshot.ParentExternalId is null
                        ? null
                        : await dbContext.Organizations.FirstOrDefaultAsync(
                            x => x.TenantId == tenantId
                                 && x.IdentitySourceId == source.Id
                                 && x.ExternalId == snapshot.ParentExternalId,
                            cancellationToken);

                    dbContext.Organizations.Add(new OrganizationEntity
                    {
                        TenantId = tenantId,
                        Name = snapshot.Name,
                        Code = $"{source.Code}-{snapshot.ExternalId}",
                        ExternalId = snapshot.ExternalId,
                        IdentitySourceId = source.Id,
                        ParentId = parent?.Id,
                        Path = parent is null ? "/" : $"{parent.Path}{parent.Id}/",
                        DisplayPath = parent is null ? $"/{snapshot.Name}" : $"{parent.DisplayPath}/{snapshot.Name}",
                        DataOrigin = source.ProviderType switch
                        {
                            IdentitySourceProviderType.DingTalk => DataOriginType.DingTalk,
                            IdentitySourceProviderType.Feishu => DataOriginType.Feishu,
                            IdentitySourceProviderType.WeCom => DataOriginType.WeCom,
                            _ => DataOriginType.Local
                        }
                    });
                    result.CreatedOrganizations++;
                }
                else
                {
                    existing.Name = snapshot.Name;
                    existing.UpdateTime = DateTimeOffset.UtcNow;
                    result.UpdatedOrganizations++;
                }
            }

            var users = await provider.PullUsersAsync(source, cancellationToken);
            foreach (var snapshot in users)
            {
                var existing = await userManager.Users
                    .FirstOrDefaultAsync(
                        x => x.TenantId == tenantId && x.ExternalId == snapshot.ExternalId && !x.IsDeleted,
                        cancellationToken);

                if (existing is null)
                {
                    var user = new NetIamIdentityUser
                    {
                        TenantId = tenantId,
                        UserName = snapshot.Username,
                        DisplayName = snapshot.DisplayName,
                        Email = snapshot.Email,
                        PhoneNumber = snapshot.Mobile,
                        ExternalId = snapshot.ExternalId,
                        DataOrigin = source.ProviderType switch
                        {
                            IdentitySourceProviderType.DingTalk => DataOriginType.DingTalk,
                            IdentitySourceProviderType.Feishu => DataOriginType.Feishu,
                            IdentitySourceProviderType.WeCom => DataOriginType.WeCom,
                            _ => DataOriginType.Local
                        },
                        EmailConfirmed = false
                    };

                    var createResult = await userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"Failed to create user from sync: {snapshot.Username}. {string.Join("; ", createResult.Errors.Select(e => e.Description))}");
                    }

                    result.CreatedUsers++;
                }
                else
                {
                    existing.DisplayName = snapshot.DisplayName;
                    existing.Email = snapshot.Email;
                    existing.PhoneNumber = snapshot.Mobile;
                    existing.UpdateTime = DateTimeOffset.UtcNow;
                    await userManager.UpdateAsync(existing);
                    result.UpdatedUsers++;
                }
            }

            history.TotalUsers = users.Count;
            history.CreatedUsers = result.CreatedUsers;
            history.UpdatedUsers = result.UpdatedUsers;
            history.Status = SyncStatus.Success;
            history.EndedTime = DateTimeOffset.UtcNow;
            history.UpdateTime = DateTimeOffset.UtcNow;

            dbContext.IdentitySourceSyncRecords.Add(new IdentitySourceSyncRecordEntity
            {
                TenantId = tenantId,
                SyncHistoryId = history.Id,
                ObjectType = "summary",
                ObjectId = source.Code,
                Action = "pull",
                Result = "success",
                Detail = $"CreatedUsers={result.CreatedUsers}, UpdatedUsers={result.UpdatedUsers}, CreatedOrganizations={result.CreatedOrganizations}, UpdatedOrganizations={result.UpdatedOrganizations}"
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await auditService.WriteAsync(
                new AuditWriteRequest(
                    tenantId,
                    "directory.sync.completed",
                    $"Identity source {identitySourceCode} sync completed.",
                    TargetJson: $$"""{"source":"{{identitySourceCode}}"}"""),
                cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            history.Status = SyncStatus.Failed;
            history.ErrorMessage = ex.Message;
            history.EndedTime = DateTimeOffset.UtcNow;
            history.UpdateTime = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            await auditService.WriteAsync(
                new AuditWriteRequest(
                    tenantId,
                    "directory.sync.failed",
                    $"Identity source {identitySourceCode} sync failed: {ex.Message}",
                    "failed"),
                cancellationToken);
            throw;
        }
    }

    public async Task<bool> HandleWebhookAsync(
        string identitySourceCode,
        string tenantId,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var source = await dbContext.IdentitySources
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == identitySourceCode && !x.IsDeleted, cancellationToken);
        if (source is null)
        {
            return false;
        }

        var provider = providerFactory.Resolve(source.ProviderType);
        var normalizedEvent = await provider.NormalizeWebhookAsync(source, payload, cancellationToken);
        if (normalizedEvent is null)
        {
            return false;
        }

        dbContext.IdentitySourceSyncRecords.Add(new IdentitySourceSyncRecordEntity
        {
            TenantId = tenantId,
            SyncHistoryId = string.Empty,
            ObjectType = "webhook",
            ObjectId = normalizedEvent.ExternalId,
            Action = normalizedEvent.EventType,
            Result = "received",
            Detail = normalizedEvent.PayloadJson
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditService.WriteAsync(
            new AuditWriteRequest(
                tenantId,
                "directory.webhook.received",
                $"Webhook received from identity source {identitySourceCode}.",
                TargetJson: normalizedEvent.PayloadJson),
            cancellationToken);

        return true;
    }
}
