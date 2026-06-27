using System.Text.Json;
using NetIAM.Domain.Contracts;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Integrations.Abstractions;
using NetIAM.Integrations.Internal;

namespace NetIAM.Integrations.Providers;

public sealed class DingTalkDirectorySyncProvider : IDirectorySyncProvider
{
    public IdentitySourceProviderType ProviderType => IdentitySourceProviderType.DingTalk;

    public Task<IReadOnlyCollection<DirectoryOrganizationSnapshot>> PullOrganizationsAsync(
        IdentitySourceEntity identitySource,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ReadOrganizationsFromConfig(identitySource));
    }

    public Task<IReadOnlyCollection<DirectoryUserSnapshot>> PullUsersAsync(
        IdentitySourceEntity identitySource,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ReadUsersFromConfig(identitySource));
    }

    public Task<DirectoryNormalizedEvent?> NormalizeWebhookAsync(
        IdentitySourceEntity identitySource,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var json = JsonDocument.Parse(payload).RootElement;
        var eventType = json.TryGetProperty("EventType", out var eventTypeValue)
            ? eventTypeValue.GetString() ?? "unknown"
            : "unknown";
        var externalId = json.TryGetProperty("UserId", out var userId)
            ? userId.GetString() ?? string.Empty
            : json.TryGetProperty("DeptId", out var deptId) ? deptId.GetString() ?? string.Empty : string.Empty;

        return Task.FromResult<DirectoryNormalizedEvent?>(
            new DirectoryNormalizedEvent(eventType, externalId, payload));
    }

    private static IReadOnlyCollection<DirectoryOrganizationSnapshot> ReadOrganizationsFromConfig(IdentitySourceEntity source)
    {
        var config = ProviderConfigParser.ParseIdentitySourceConfig(source);
        if (!config.TryGetProperty("mockOrganizations", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<DirectoryOrganizationSnapshot>();
        foreach (var node in nodes.EnumerateArray())
        {
            result.Add(new DirectoryOrganizationSnapshot(
                node.GetProperty("externalId").GetString() ?? string.Empty,
                node.GetProperty("name").GetString() ?? string.Empty,
                node.TryGetProperty("parentExternalId", out var parent) ? parent.GetString() : null));
        }

        return result;
    }

    private static IReadOnlyCollection<DirectoryUserSnapshot> ReadUsersFromConfig(IdentitySourceEntity source)
    {
        var config = ProviderConfigParser.ParseIdentitySourceConfig(source);
        if (!config.TryGetProperty("mockUsers", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<DirectoryUserSnapshot>();
        foreach (var node in nodes.EnumerateArray())
        {
            result.Add(new DirectoryUserSnapshot(
                node.GetProperty("externalId").GetString() ?? string.Empty,
                node.GetProperty("username").GetString() ?? string.Empty,
                node.GetProperty("displayName").GetString() ?? string.Empty,
                node.TryGetProperty("email", out var email) ? email.GetString() : null,
                node.TryGetProperty("mobile", out var mobile) ? mobile.GetString() : null,
                node.TryGetProperty("departmentExternalId", out var dep) ? dep.GetString() : null));
        }

        return result;
    }
}
