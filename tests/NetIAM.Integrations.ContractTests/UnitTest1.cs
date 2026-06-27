using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Integrations.Providers;

namespace NetIAM.Integrations.ContractTests;

public sealed class ProviderSandboxContractTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public async Task DingTalkDirectorySync_ShouldPullOrganizationsAndUsers()
    {
        if (!TryRequireEnvironment("NETIAM_DINGTALK_APP_KEY", out var appKey)
            || !TryRequireEnvironment("NETIAM_DINGTALK_APP_SECRET", out var appSecret))
        {
            return;
        }

        var rootDeptId = Environment.GetEnvironmentVariable("NETIAM_DINGTALK_ROOT_DEPT_ID") ?? "1";

        var provider = new DingTalkDirectorySyncProvider(new HttpClient());
        var source = CreateSource(
            "contract-dingtalk",
            IdentitySourceProviderType.DingTalk,
            new
            {
                appKey,
                appSecret,
                rootDeptId,
                useMock = false
            });

        var organizations = await provider.PullOrganizationsAsync(source);
        var users = await provider.PullUsersAsync(source);

        Assert.NotNull(organizations);
        Assert.NotNull(users);
        Assert.All(organizations, organization => Assert.False(string.IsNullOrWhiteSpace(organization.ExternalId)));
        Assert.All(users, user => Assert.False(string.IsNullOrWhiteSpace(user.ExternalId)));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task FeishuDirectorySync_ShouldPullOrganizationsAndUsers()
    {
        if (!TryRequireEnvironment("NETIAM_FEISHU_APP_ID", out var appId)
            || !TryRequireEnvironment("NETIAM_FEISHU_APP_SECRET", out var appSecret))
        {
            return;
        }

        var provider = new FeishuDirectorySyncProvider(new HttpClient());
        var source = CreateSource(
            "contract-feishu",
            IdentitySourceProviderType.Feishu,
            new
            {
                appId,
                appSecret,
                useMock = false
            });

        var organizations = await provider.PullOrganizationsAsync(source);
        var users = await provider.PullUsersAsync(source);

        Assert.NotNull(organizations);
        Assert.NotNull(users);
        Assert.All(organizations, organization => Assert.False(string.IsNullOrWhiteSpace(organization.ExternalId)));
        Assert.All(users, user => Assert.False(string.IsNullOrWhiteSpace(user.ExternalId)));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task WeComDirectorySync_ShouldPullOrganizationsAndUsers()
    {
        if (!TryRequireEnvironment("NETIAM_WECOM_CORP_ID", out var corpId)
            || !TryRequireEnvironment("NETIAM_WECOM_APP_SECRET", out var appSecret))
        {
            return;
        }

        var provider = new WeComDirectorySyncProvider(new HttpClient());
        var source = CreateSource(
            "contract-wecom",
            IdentitySourceProviderType.WeCom,
            new
            {
                corpId,
                appSecret,
                fetchUserDetail = false,
                useMock = false
            });

        var organizations = await provider.PullOrganizationsAsync(source);
        var users = await provider.PullUsersAsync(source);

        Assert.NotNull(organizations);
        Assert.NotNull(users);
        Assert.All(organizations, organization => Assert.False(string.IsNullOrWhiteSpace(organization.ExternalId)));
        Assert.All(users, user => Assert.False(string.IsNullOrWhiteSpace(user.ExternalId)));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task WeComAuth_ShouldExchangeAndLoadProfile_WhenAuthorizationCodeProvided()
    {
        if (!TryRequireEnvironment("NETIAM_WECOM_CORP_ID", out var corpId)
            || !TryRequireEnvironment("NETIAM_WECOM_AGENT_ID", out var agentId)
            || !TryRequireEnvironment("NETIAM_WECOM_APP_SECRET", out var appSecret)
            || !TryRequireEnvironment("NETIAM_WECOM_AUTH_CODE", out var authorizationCode))
        {
            return;
        }

        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var provider = new WeComAuthProviderHandler(new HttpClient(), memoryCache);
        var identityProvider = new IdentityProviderEntity
        {
            TenantId = "tenant-default",
            Code = "contract-wecom-auth",
            Name = "contract-wecom-auth",
            ProviderType = ExternalProviderType.WeCom,
            ConfigJson = JsonSerializer.Serialize(new { corpId, agentId, appSecret }),
            Enabled = true
        };

        var token = await provider.ExchangeTokenAsync(
            identityProvider,
            new NetIAM.Domain.Contracts.ExternalAuthCallback(
                "tenant-default",
                identityProvider.Code,
                authorizationCode,
                "contract-state",
                "https://localhost/callback"));
        var profile = await provider.GetUserProfileAsync(identityProvider, token);

        Assert.False(string.IsNullOrWhiteSpace(profile.OpenId));
    }

    private static IdentitySourceEntity CreateSource(string code, IdentitySourceProviderType providerType, object config)
    {
        return new IdentitySourceEntity
        {
            TenantId = "tenant-default",
            Code = code,
            Name = code,
            ProviderType = providerType,
            BasicConfigJson = JsonSerializer.Serialize(config),
            StrategyConfigJson = "{}",
            JobConfigJson = "{}",
            Enabled = true
        };
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
}
