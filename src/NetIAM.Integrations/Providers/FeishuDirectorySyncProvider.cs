using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NetIAM.Domain.Contracts;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Integrations.Abstractions;
using NetIAM.Integrations.Internal;

namespace NetIAM.Integrations.Providers;

public sealed class FeishuDirectorySyncProvider(HttpClient httpClient) : IDirectorySyncProvider
{
    public IdentitySourceProviderType ProviderType => IdentitySourceProviderType.Feishu;

    public async Task<IReadOnlyCollection<DirectoryOrganizationSnapshot>> PullOrganizationsAsync(
        IdentitySourceEntity identitySource,
        CancellationToken cancellationToken = default)
    {
        var config = ProviderConfigParser.ParseIdentitySourceConfig(identitySource);
        if (UseMock(config) || !HasCredentials(config, "appId", "appSecret"))
        {
            return ReadOrganizationsFromConfig(config);
        }

        var appId = ProviderConfigParser.RequiredString(config, "appId");
        var appSecret = ProviderConfigParser.RequiredString(config, "appSecret");
        var token = await GetTenantAccessTokenAsync(appId, appSecret, cancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://open.feishu.cn/open-apis/contact/v3/departments?page_size=100&fetch_child=true");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement;
        ThrowIfApiError(payload);

        if (!payload.TryGetProperty("data", out var dataNode)
            || !dataNode.TryGetProperty("items", out var itemsNode)
            || itemsNode.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<DirectoryOrganizationSnapshot>();
        foreach (var node in itemsNode.EnumerateArray())
        {
            var externalId = node.TryGetProperty("department_id", out var idNode) ? idNode.GetString() : null;
            if (string.IsNullOrWhiteSpace(externalId))
            {
                continue;
            }

            var name = node.TryGetProperty("name", out var nameNode) ? nameNode.GetString() ?? externalId : externalId;
            var parentId = node.TryGetProperty("parent_department_id", out var parentNode) ? parentNode.GetString() : null;
            result.Add(new DirectoryOrganizationSnapshot(externalId, name, parentId));
        }

        return result;
    }

    public async Task<IReadOnlyCollection<DirectoryUserSnapshot>> PullUsersAsync(
        IdentitySourceEntity identitySource,
        CancellationToken cancellationToken = default)
    {
        var config = ProviderConfigParser.ParseIdentitySourceConfig(identitySource);
        if (UseMock(config) || !HasCredentials(config, "appId", "appSecret"))
        {
            return ReadUsersFromConfig(config);
        }

        var appId = ProviderConfigParser.RequiredString(config, "appId");
        var appSecret = ProviderConfigParser.RequiredString(config, "appSecret");
        var token = await GetTenantAccessTokenAsync(appId, appSecret, cancellationToken);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://open.feishu.cn/open-apis/contact/v3/users?page_size=100");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement;
        ThrowIfApiError(payload);

        if (!payload.TryGetProperty("data", out var dataNode)
            || !dataNode.TryGetProperty("items", out var itemsNode)
            || itemsNode.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<DirectoryUserSnapshot>();
        foreach (var node in itemsNode.EnumerateArray())
        {
            var externalId = node.TryGetProperty("user_id", out var idNode) ? idNode.GetString() : null;
            if (string.IsNullOrWhiteSpace(externalId))
            {
                continue;
            }

            var username = node.TryGetProperty("email", out var emailNode) && !string.IsNullOrWhiteSpace(emailNode.GetString())
                ? emailNode.GetString()!
                : externalId;
            var displayName = node.TryGetProperty("name", out var displayNameNode) ? displayNameNode.GetString() ?? username : username;
            var departmentExternalId = node.TryGetProperty("department_ids", out var departmentsNode)
                                       && departmentsNode.ValueKind == JsonValueKind.Array
                ? departmentsNode.EnumerateArray().FirstOrDefault().GetString()
                : null;

            result.Add(new DirectoryUserSnapshot(
                externalId,
                username,
                displayName,
                node.TryGetProperty("email", out var email) ? email.GetString() : null,
                node.TryGetProperty("mobile", out var mobile) ? mobile.GetString() : null,
                departmentExternalId));
        }

        return result;
    }

    public Task<DirectoryNormalizedEvent?> NormalizeWebhookAsync(
        IdentitySourceEntity identitySource,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var json = JsonDocument.Parse(payload).RootElement;

        if (json.TryGetProperty("challenge", out var challengeNode))
        {
            return Task.FromResult<DirectoryNormalizedEvent?>(
                new DirectoryNormalizedEvent("challenge", challengeNode.GetString() ?? string.Empty, payload));
        }

        var eventType = json.TryGetProperty("type", out var eventTypeValue)
            ? eventTypeValue.GetString() ?? "unknown"
            : "unknown";
        var externalId = json.TryGetProperty("open_id", out var openId)
            ? openId.GetString() ?? string.Empty
            : json.TryGetProperty("department_id", out var deptId) ? deptId.GetString() ?? string.Empty : string.Empty;

        return Task.FromResult<DirectoryNormalizedEvent?>(
            new DirectoryNormalizedEvent(eventType, externalId, payload));
    }

    private async Task<string> GetTenantAccessTokenAsync(string appId, string appSecret, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "https://open.feishu.cn/open-apis/auth/v3/tenant_access_token/internal",
            new { app_id = appId, app_secret = appSecret },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement;
        ThrowIfApiError(payload);
        return payload.GetProperty("tenant_access_token").GetString()
               ?? throw new InvalidOperationException("Feishu tenant_access_token missing.");
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
        if (payload.TryGetProperty("code", out var codeNode))
        {
            var code = codeNode.GetInt32();
            if (code != 0)
            {
                var message = payload.TryGetProperty("msg", out var messageNode) ? messageNode.GetString() : "unknown";
                throw new InvalidOperationException($"Feishu API error ({code}): {message}");
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
