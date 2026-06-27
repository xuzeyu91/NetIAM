using System.Text.Json;
using NetIAM.Domain.Contracts;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Integrations.Abstractions;
using NetIAM.Integrations.Internal;

namespace NetIAM.Integrations.Providers;

public sealed class DingTalkDirectorySyncProvider(HttpClient httpClient) : IDirectorySyncProvider
{
    public IdentitySourceProviderType ProviderType => IdentitySourceProviderType.DingTalk;

    public async Task<IReadOnlyCollection<DirectoryOrganizationSnapshot>> PullOrganizationsAsync(
        IdentitySourceEntity identitySource,
        CancellationToken cancellationToken = default)
    {
        var config = ProviderConfigParser.ParseIdentitySourceConfig(identitySource);
        if (UseMock(config) || !HasCredentials(config, "appKey", "appSecret"))
        {
            return ReadOrganizationsFromConfig(config);
        }

        var appKey = ProviderConfigParser.RequiredString(config, "appKey");
        var appSecret = ProviderConfigParser.RequiredString(config, "appSecret");
        var rootDeptId = ProviderConfigParser.OptionalInt(config, "rootDeptId", 1);

        var accessToken = await GetAccessTokenAsync(appKey, appSecret, cancellationToken);

        using var response = await httpClient.PostJsonAsyncWithGovernance(
            "dingtalk-sync",
            $"https://oapi.dingtalk.com/topapi/v2/department/listsub?access_token={Uri.EscapeDataString(accessToken)}",
            new { dept_id = rootDeptId },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement;
        ThrowIfApiError(payload);

        var result = new List<DirectoryOrganizationSnapshot>
        {
            new(rootDeptId.ToString(), ProviderConfigParser.OptionalString(config, "rootDeptName", "Root"), null)
        };
        if (!payload.TryGetProperty("result", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var node in nodes.EnumerateArray())
        {
            var externalId = node.GetProperty("dept_id").ToString();
            var name = node.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? externalId : externalId;
            var parentId = node.TryGetProperty("parent_id", out var parentValue) ? parentValue.ToString() : rootDeptId.ToString();
            result.Add(new DirectoryOrganizationSnapshot(externalId, name, parentId == rootDeptId.ToString() ? rootDeptId.ToString() : parentId));
        }

        return result;
    }

    public async Task<IReadOnlyCollection<DirectoryUserSnapshot>> PullUsersAsync(
        IdentitySourceEntity identitySource,
        CancellationToken cancellationToken = default)
    {
        var config = ProviderConfigParser.ParseIdentitySourceConfig(identitySource);
        if (UseMock(config) || !HasCredentials(config, "appKey", "appSecret"))
        {
            return ReadUsersFromConfig(config);
        }

        var appKey = ProviderConfigParser.RequiredString(config, "appKey");
        var appSecret = ProviderConfigParser.RequiredString(config, "appSecret");
        var accessToken = await GetAccessTokenAsync(appKey, appSecret, cancellationToken);
        var organizations = await PullOrganizationsAsync(identitySource, cancellationToken);
        var userMap = new Dictionary<string, DirectoryUserSnapshot>(StringComparer.OrdinalIgnoreCase);

        foreach (var organization in organizations.Where(x => !string.IsNullOrWhiteSpace(x.ExternalId)))
        {
            using var response = await httpClient.PostJsonAsyncWithGovernance(
                "dingtalk-sync",
                $"https://oapi.dingtalk.com/topapi/v2/user/list?access_token={Uri.EscapeDataString(accessToken)}",
                new { dept_id = organization.ExternalId, cursor = 0, size = 100 },
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement;
            ThrowIfApiError(payload);

            if (!payload.TryGetProperty("result", out var resultNode)
                || !resultNode.TryGetProperty("list", out var usersNode)
                || usersNode.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var userNode in usersNode.EnumerateArray())
            {
                var externalId = userNode.TryGetProperty("userid", out var userIdNode)
                    ? userIdNode.GetString()
                    : null;
                if (string.IsNullOrWhiteSpace(externalId))
                {
                    continue;
                }

                var username = externalId;
                var displayName = userNode.TryGetProperty("name", out var displayNameNode)
                    ? displayNameNode.GetString() ?? externalId
                    : externalId;

                userMap[externalId] = new DirectoryUserSnapshot(
                    externalId,
                    username,
                    displayName,
                    userNode.TryGetProperty("email", out var email) ? email.GetString() : null,
                    userNode.TryGetProperty("mobile", out var mobile) ? mobile.GetString() : null,
                    organization.ExternalId);
            }
        }

        return userMap.Values.ToArray();
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

    private async Task<string> GetAccessTokenAsync(string appKey, string appSecret, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsyncWithGovernance(
            "dingtalk-sync",
            $"https://oapi.dingtalk.com/gettoken?appkey={Uri.EscapeDataString(appKey)}&appsecret={Uri.EscapeDataString(appSecret)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement;
        ThrowIfApiError(payload);
        return payload.GetProperty("access_token").GetString()
               ?? throw new InvalidOperationException("DingTalk access_token missing.");
    }

    private static bool UseMock(JsonElement config)
    {
        return ProviderConfigParser.OptionalBoolean(config, "useMock", false);
    }

    private static bool HasCredentials(JsonElement config, string keyA, string keyB)
    {
        return !string.IsNullOrWhiteSpace(ProviderConfigParser.OptionalString(config, keyA))
               && !string.IsNullOrWhiteSpace(ProviderConfigParser.OptionalString(config, keyB));
    }

    private static void ThrowIfApiError(JsonElement payload)
    {
        if (payload.TryGetProperty("errcode", out var errCodeNode))
        {
            var errCode = errCodeNode.GetInt32();
            if (errCode != 0)
            {
                var errMessage = payload.TryGetProperty("errmsg", out var errMessageNode) ? errMessageNode.GetString() : "unknown";
                throw new InvalidOperationException($"DingTalk API error ({errCode}): {errMessage}");
            }
        }
    }

    private static IReadOnlyCollection<DirectoryOrganizationSnapshot> ReadOrganizationsFromConfig(JsonElement config)
    {
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

    private static IReadOnlyCollection<DirectoryUserSnapshot> ReadUsersFromConfig(JsonElement config)
    {
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
