using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using NetIAM.Domain.Contracts;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Integrations.Abstractions;
using NetIAM.Integrations.Internal;

namespace NetIAM.Integrations.Providers;

public sealed class WeComAuthProviderHandler(HttpClient httpClient, IMemoryCache memoryCache) : IExternalAuthProviderHandler
{
    private const string TokenApi = "https://qyapi.weixin.qq.com/cgi-bin/gettoken";
    private const string UserInfoApi = "https://qyapi.weixin.qq.com/cgi-bin/user/getuserinfo";

    public ExternalProviderType ProviderType => ExternalProviderType.WeCom;

    public Task<string> BuildAuthorizeUrlAsync(
        IdentityProviderEntity provider,
        ExternalAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var config = ProviderConfigParser.ParseIdentityProviderConfig(provider);
        var corpId = ProviderConfigParser.RequiredString(config, "corpId");
        var agentId = ProviderConfigParser.RequiredString(config, "agentId");
        var redirectUri = UrlEncoder.Default.Encode($"{request.CallbackBaseUri.TrimEnd('/')}/login/wecom/{provider.Code}");
        var state = UrlEncoder.Default.Encode(request.State);
        var authorizeUrl =
            $"https://open.work.weixin.qq.com/wwopen/sso/qrConnect?appid={corpId}&agentid={agentId}&redirect_uri={redirectUri}&state={state}";

        return Task.FromResult(authorizeUrl);
    }

    public async Task<ExternalUserAccessToken> ExchangeTokenAsync(
        IdentityProviderEntity provider,
        ExternalAuthCallback callback,
        CancellationToken cancellationToken = default)
    {
        var config = ProviderConfigParser.ParseIdentityProviderConfig(provider);
        var corpId = ProviderConfigParser.RequiredString(config, "corpId");
        var appSecret = ProviderConfigParser.RequiredString(config, "appSecret");

        var cacheKey = $"wecom:corp-token:{corpId}:{appSecret}";
        var corpToken = await memoryCache.GetOrCreateAsync(
            cacheKey,
            async entry =>
            {
                using var tokenResponse = await httpClient.GetAsync(
                    $"{TokenApi}?corpid={Uri.EscapeDataString(corpId)}&corpsecret={Uri.EscapeDataString(appSecret)}",
                    cancellationToken);
                tokenResponse.EnsureSuccessStatusCode();
                var tokenJson = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken)).RootElement;
                var accessToken = tokenJson.GetProperty("access_token").GetString()
                    ?? throw new InvalidOperationException("WeCom corp access_token missing.");
                var expiresIn = tokenJson.TryGetProperty("expires_in", out var expiresValue) ? expiresValue.GetInt32() : 7200;
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(60, expiresIn - 120));
                return accessToken;
            });

        using var userInfoResponse = await httpClient.GetAsync(
            $"{UserInfoApi}?access_token={Uri.EscapeDataString(corpToken!)}&code={Uri.EscapeDataString(callback.AuthorizationCode)}",
            cancellationToken);
        userInfoResponse.EnsureSuccessStatusCode();
        var userInfoRaw = await userInfoResponse.Content.ReadAsStringAsync(cancellationToken);
        var userInfoJson = JsonDocument.Parse(userInfoRaw).RootElement;

        var openId = userInfoJson.TryGetProperty("UserId", out var userIdValue)
            ? userIdValue.GetString()
            : userInfoJson.TryGetProperty("OpenId", out var openIdValue) ? openIdValue.GetString() : null;
        if (string.IsNullOrWhiteSpace(openId))
        {
            throw new InvalidOperationException("WeCom callback missing UserId/OpenId.");
        }

        // WeCom login is code + corp token based; return synthetic token with short TTL.
        return new ExternalUserAccessToken(
            $"{corpToken}:{openId}",
            DateTimeOffset.UtcNow.AddMinutes(5));
    }

    public Task<ExternalUserProfile> GetUserProfileAsync(
        IdentityProviderEntity provider,
        ExternalUserAccessToken accessToken,
        CancellationToken cancellationToken = default)
    {
        var tokenParts = accessToken.AccessToken.Split(':', 2);
        var openId = tokenParts.Length == 2 ? tokenParts[1] : accessToken.AccessToken;
        var rawJson = $$"""{"openId":"{{openId}}"}""";
        return Task.FromResult(new ExternalUserProfile(openId, null, null, null, null, null, rawJson));
    }
}
