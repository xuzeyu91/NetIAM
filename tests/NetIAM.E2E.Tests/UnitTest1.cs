using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NetIAM.E2E.Tests;

public sealed class AdminE2ETests
{
    [Fact]
    [Trait("Category", "E2E")]
    public async Task AdminIdentitySourceSync_ShouldRunEndToEnd_WithSandboxCredentials()
    {
        if (!TryRequireEnvironment("NETIAM_E2E_ADMIN_BASE_URL", out var baseUrl)
            || !TryRequireEnvironment("NETIAM_E2E_TENANT_ID", out var tenantId)
            || !TryRequireEnvironment("NETIAM_E2E_ACTING_USER_ID", out var actingUserId))
        {
            return;
        }

        var providerCode = (Environment.GetEnvironmentVariable("NETIAM_E2E_PROVIDER") ?? "dingtalk").Trim().ToLowerInvariant();

        var sourceCode = $"e2e-{providerCode}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        using var httpClient = CreateClient(baseUrl, tenantId, actingUserId);

        var createPayload = providerCode switch
        {
            "dingtalk" => BuildSourceCreatePayload(
                sourceCode,
                "DingTalk",
                1,
                new
                {
                    appKey = GetRequiredOrDefault("NETIAM_DINGTALK_APP_KEY"),
                    appSecret = GetRequiredOrDefault("NETIAM_DINGTALK_APP_SECRET"),
                    rootDeptId = Environment.GetEnvironmentVariable("NETIAM_DINGTALK_ROOT_DEPT_ID") ?? "1",
                    useMock = false
                }),
            "feishu" => BuildSourceCreatePayload(
                sourceCode,
                "Feishu",
                2,
                new
                {
                    appId = GetRequiredOrDefault("NETIAM_FEISHU_APP_ID"),
                    appSecret = GetRequiredOrDefault("NETIAM_FEISHU_APP_SECRET"),
                    useMock = false
                }),
            "wecom" => BuildSourceCreatePayload(
                sourceCode,
                "WeCom",
                3,
                new
                {
                    corpId = GetRequiredOrDefault("NETIAM_WECOM_CORP_ID"),
                    appSecret = GetRequiredOrDefault("NETIAM_WECOM_APP_SECRET"),
                    fetchUserDetail = false,
                    useMock = false
                }),
            _ => BuildSourceCreatePayload(sourceCode, "Unsupported", 1, new { useMock = true })
        };
        if (providerCode is not ("dingtalk" or "feishu" or "wecom"))
        {
            return;
        }

        if (ContainsMissingCredentials(createPayload))
        {
            return;
        }

        try
        {
            await SendJsonAsync(httpClient, HttpMethod.Post, "/api/admin/identity-sources", createPayload);
            await SendJsonAsync(httpClient, HttpMethod.Post, $"/api/admin/identity-sources/{Uri.EscapeDataString(sourceCode)}/sync", null);

            var historiesText = await SendTextAsync(
                httpClient,
                HttpMethod.Get,
                $"/api/admin/identity-sources/{Uri.EscapeDataString(sourceCode)}/sync-histories?take=5");
            var histories = JsonDocument.Parse(historiesText).RootElement;

            Assert.Equal(JsonValueKind.Array, histories.ValueKind);
            Assert.True(histories.GetArrayLength() > 0, "Expected at least one sync history record.");
        }
        finally
        {
            await SendTextAsync(
                httpClient,
                HttpMethod.Delete,
                $"/api/admin/identity-sources/{Uri.EscapeDataString(sourceCode)}",
                allowFailure: true);
        }
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task SessionRevocation_ShouldBlockSubsequentRequests_WithSameSessionId()
    {
        if (!TryRequireEnvironment("NETIAM_E2E_ADMIN_BASE_URL", out var baseUrl)
            || !TryRequireEnvironment("NETIAM_E2E_TENANT_ID", out var tenantId)
            || !TryRequireEnvironment("NETIAM_E2E_ACTING_USER_ID", out var actingUserId))
        {
            return;
        }

        var syntheticSessionId = $"e2e-session-{Guid.NewGuid():N}";

        using var httpClient = CreateClient(baseUrl, tenantId, actingUserId, syntheticSessionId);
        await SendJsonAsync(
            httpClient,
            HttpMethod.Post,
            $"/api/admin/monitor/sessions/{Uri.EscapeDataString(syntheticSessionId)}/revoke",
            null);

        using var blockedResponse = await httpClient.GetAsync("/api/admin/audit-events?take=1");
        Assert.Equal(HttpStatusCode.Unauthorized, blockedResponse.StatusCode);
    }

    private static object BuildSourceCreatePayload(string code, string name, int providerType, object basicConfig)
    {
        return new
        {
            code,
            name,
            providerType,
            enabled = true,
            basicConfigJson = JsonSerializer.Serialize(basicConfig),
            strategyConfigJson = "{}",
            jobConfigJson = "{}"
        };
    }

    private static HttpClient CreateClient(string baseUrl, string tenantId, string actingUserId, string? sessionId = null)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl, UriKind.Absolute)
        };
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        client.DefaultRequestHeaders.Add("X-Acting-User-Id", actingUserId);

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            client.DefaultRequestHeaders.Add("X-Session-Id", sessionId);
        }

        var bearerToken = Environment.GetEnvironmentVariable("NETIAM_E2E_BEARER_TOKEN");
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            var normalizedToken = bearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? bearerToken
                : $"Bearer {bearerToken}";
            client.DefaultRequestHeaders.Add("Authorization", normalizedToken);
        }

        return client;
    }

    private static async Task<string> SendTextAsync(
        HttpClient httpClient,
        HttpMethod method,
        string path,
        bool allowFailure = false)
    {
        using var request = new HttpRequestMessage(method, path);
        using var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!allowFailure && !response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Request failed ({(int)response.StatusCode}): {content}");
        }

        return content;
    }

    private static async Task SendJsonAsync(
        HttpClient httpClient,
        HttpMethod method,
        string path,
        object? payload)
    {
        using var request = new HttpRequestMessage(method, path);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        using var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Request failed ({(int)response.StatusCode}): {content}");
        }
    }

    private static bool TryRequireEnvironment(string key, out string value)
    {
        value = Environment.GetEnvironmentVariable(key) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            value = string.Empty;
            return false;
        }

        return true;
    }

    private static string GetRequiredOrDefault(string key)
    {
        return Environment.GetEnvironmentVariable(key) ?? "__MISSING__";
    }

    private static bool ContainsMissingCredentials(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return json.Contains("__MISSING__", StringComparison.Ordinal);
    }
}
