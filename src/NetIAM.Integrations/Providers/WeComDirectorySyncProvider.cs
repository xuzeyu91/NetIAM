using System.Text.Json;
using NetIAM.Domain.Contracts;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Integrations.Abstractions;
using NetIAM.Integrations.Internal;

namespace NetIAM.Integrations.Providers;

public sealed class WeComDirectorySyncProvider(HttpClient httpClient) : IDirectorySyncProvider
{
    public IdentitySourceProviderType ProviderType => IdentitySourceProviderType.WeCom;

    public async Task<IReadOnlyCollection<DirectoryOrganizationSnapshot>> PullOrganizationsAsync(
        IdentitySourceEntity identitySource,
        CancellationToken cancellationToken = default)
    {
        var config = ProviderConfigParser.ParseIdentitySourceConfig(identitySource);
        if (UseMock(config) || !HasCredentials(config, "corpId", "appSecret", "corpSecret"))
        {
            return ReadOrganizationsFromConfig(config);
        }

        var corpId = ProviderConfigParser.RequiredString(config, "corpId");
        var appSecret = ProviderConfigParser.RequiredStringAny(config, "appSecret", "corpSecret");
        var token = await GetAccessTokenAsync(corpId, appSecret, cancellationToken);

        using var response = await httpClient.GetAsync(
            $"https://qyapi.weixin.qq.com/cgi-bin/department/list?access_token={Uri.EscapeDataString(token)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement;
        ThrowIfApiError(payload);

        if (!payload.TryGetProperty("department", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<DirectoryOrganizationSnapshot>();
        foreach (var node in nodes.EnumerateArray())
        {
            var id = node.TryGetProperty("id", out var idNode) ? idNode.ToString() : null;
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var name = node.TryGetProperty("name", out var nameNode) ? nameNode.GetString() ?? id : id;
            var parentId = node.TryGetProperty("parentid", out var parentNode) ? parentNode.ToString() : null;
            result.Add(new DirectoryOrganizationSnapshot(id, name, parentId == "0" ? null : parentId));
        }

        return result;
    }

    public async Task<IReadOnlyCollection<DirectoryUserSnapshot>> PullUsersAsync(
        IdentitySourceEntity identitySource,
        CancellationToken cancellationToken = default)
    {
        var config = ProviderConfigParser.ParseIdentitySourceConfig(identitySource);
        if (UseMock(config) || !HasCredentials(config, "corpId", "appSecret", "corpSecret"))
        {
            return ReadUsersFromConfig(config);
        }

        var corpId = ProviderConfigParser.RequiredString(config, "corpId");
        var appSecret = ProviderConfigParser.RequiredStringAny(config, "appSecret", "corpSecret");
        var token = await GetAccessTokenAsync(corpId, appSecret, cancellationToken);
        var organizations = await PullOrganizationsAsync(identitySource, cancellationToken);
        var fetchUserDetail = ProviderConfigParser.OptionalBoolean(config, "fetchUserDetail", true);
        var userMap = new Dictionary<string, DirectoryUserSnapshot>(StringComparer.OrdinalIgnoreCase);

        foreach (var organization in organizations)
        {
            using var response = await httpClient.GetAsync(
                $"https://qyapi.weixin.qq.com/cgi-bin/user/simplelist?access_token={Uri.EscapeDataString(token)}&department_id={Uri.EscapeDataString(organization.ExternalId)}&fetch_child=0",
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement;
            ThrowIfApiError(payload);

            if (!payload.TryGetProperty("userlist", out var userNodes) || userNodes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var userNode in userNodes.EnumerateArray())
            {
                var userId = userNode.TryGetProperty("userid", out var userIdNode) ? userIdNode.GetString() : null;
                if (string.IsNullOrWhiteSpace(userId))
                {
                    continue;
                }

                var name = userNode.TryGetProperty("name", out var nameNode) ? nameNode.GetString() ?? userId : userId;
                var email = string.Empty;
                string? mobile = null;

                if (fetchUserDetail)
                {
                    using var userDetailResponse = await httpClient.GetAsync(
                        $"https://qyapi.weixin.qq.com/cgi-bin/user/get?access_token={Uri.EscapeDataString(token)}&userid={Uri.EscapeDataString(userId)}",
                        cancellationToken);
                    userDetailResponse.EnsureSuccessStatusCode();
                    var userDetail = JsonDocument.Parse(await userDetailResponse.Content.ReadAsStringAsync(cancellationToken)).RootElement;
                    ThrowIfApiError(userDetail);
                    email = userDetail.TryGetProperty("email", out var emailNode) ? emailNode.GetString() ?? string.Empty : string.Empty;
                    mobile = userDetail.TryGetProperty("mobile", out var mobileNode) ? mobileNode.GetString() : null;
                }

                userMap[userId] = new DirectoryUserSnapshot(
                    userId,
                    string.IsNullOrWhiteSpace(email) ? userId : email,
                    name,
                    string.IsNullOrWhiteSpace(email) ? null : email,
                    mobile,
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
        var eventType = json.TryGetProperty("ChangeType", out var eventTypeValue)
            ? eventTypeValue.GetString() ?? "unknown"
            : "unknown";
        var externalId = json.TryGetProperty("UserID", out var userId)
            ? userId.GetString() ?? string.Empty
            : json.TryGetProperty("Id", out var id) ? id.GetString() ?? string.Empty : string.Empty;

        return Task.FromResult<DirectoryNormalizedEvent?>(
            new DirectoryNormalizedEvent(eventType, externalId, payload));
    }

    private async Task<string> GetAccessTokenAsync(string corpId, string appSecret, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(
            $"https://qyapi.weixin.qq.com/cgi-bin/gettoken?corpid={Uri.EscapeDataString(corpId)}&corpsecret={Uri.EscapeDataString(appSecret)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement;
        ThrowIfApiError(payload);
        return payload.GetProperty("access_token").GetString()
               ?? throw new InvalidOperationException("WeCom access_token missing.");
    }

    private static bool UseMock(JsonElement config)
    {
        return ProviderConfigParser.OptionalBoolean(config, "useMock", false);
    }

    private static bool HasCredentials(JsonElement config, string firstKey, params string[] secondKeys)
    {
        if (string.IsNullOrWhiteSpace(ProviderConfigParser.OptionalString(config, firstKey)))
        {
            return false;
        }

        foreach (var secondKey in secondKeys)
        {
            if (!string.IsNullOrWhiteSpace(ProviderConfigParser.OptionalString(config, secondKey)))
            {
                return true;
            }
        }

        return false;
    }

    private static void ThrowIfApiError(JsonElement payload)
    {
        if (payload.TryGetProperty("errcode", out var errCodeNode))
        {
            var errCode = errCodeNode.GetInt32();
            if (errCode != 0)
            {
                var errMessage = payload.TryGetProperty("errmsg", out var errMessageNode) ? errMessageNode.GetString() : "unknown";
                throw new InvalidOperationException($"WeCom API error ({errCode}): {errMessage}");
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
