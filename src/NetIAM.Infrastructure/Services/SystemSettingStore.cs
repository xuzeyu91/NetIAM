using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetIAM.Domain.Entities;
using NetIAM.Infrastructure.Persistence;

namespace NetIAM.Infrastructure.Services;

public interface ISystemSettingStore
{
    Task<T> GetAsync<T>(string tenantId, string settingKey, T defaultValue, CancellationToken cancellationToken = default);

    Task<T> SetAsync<T>(string tenantId, string settingKey, T value, CancellationToken cancellationToken = default);
}

public sealed class SystemSettingStore(NetIamDbContext dbContext) : ISystemSettingStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task<T> GetAsync<T>(
        string tenantId,
        string settingKey,
        T defaultValue,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SystemSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                     && x.SettingKey == settingKey
                     && !x.IsDeleted,
                cancellationToken);

        if (entity is null || string.IsNullOrWhiteSpace(entity.ValueJson))
        {
            return defaultValue;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<T>(entity.ValueJson, SerializerOptions);
            return parsed is null ? defaultValue : parsed;
        }
        catch
        {
            return defaultValue;
        }
    }

    public async Task<T> SetAsync<T>(
        string tenantId,
        string settingKey,
        T value,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.SystemSettings
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                     && x.SettingKey == settingKey
                     && !x.IsDeleted,
                cancellationToken);

        var valueJson = JsonSerializer.Serialize(value, SerializerOptions);
        if (entity is null)
        {
            entity = new SystemSettingEntity
            {
                TenantId = tenantId,
                SettingKey = settingKey,
                ValueJson = valueJson
            };
            dbContext.SystemSettings.Add(entity);
        }
        else
        {
            entity.ValueJson = valueJson;
            entity.UpdateTime = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return value;
    }
}
