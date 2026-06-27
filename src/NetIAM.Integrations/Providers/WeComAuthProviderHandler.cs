using System.Text;
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
    private const string UserDetailApi = "https://qyapi.weixin.qq.com/cgi-bin/user/get";

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
            $"https://open.work.weixin.qq.com/wwopen/sso/v1/qrConnect?appid={corpId}&agentid={agentId}&redirect_uri={redirectUri}&state={state}&login_type=jssdk";

        return Task.FromResult(authorizeUrl);
    }

    public async Task<ExternalUserAccessToken> ExchangeTokenAsync(
        IdentityProviderEntity provider,
        ExternalAuthCallback callback,
        CancellationToken cancellationToken = default)
    {
        var config = ProviderConfigParser.ParseIdentityProviderConfig(provider);
        var corpId = ProviderConfigParser.RequiredString(config, "corpId");
        var appSecret = ProviderConfigParser.RequiredStringAny(config, "appSecret", "corpSecret");

        var corpToken = await GetCorpTokenAsync(corpId, appSecret, cancellationToken);

        using var userInfoResponse = await httpClient.GetAsync(
            $"{UserInfoApi}?access_token={Uri.EscapeDataString(corpToken)}&code={Uri.EscapeDataString(callback.AuthorizationCode)}",
            cancellationToken);
        userInfoResponse.EnsureSuccessStatusCode();

        var userInfoRaw = await userInfoResponse.Content.ReadAsStringAsync(cancellationToken);
        var userInfoJson = JsonDocument.Parse(userInfoRaw).RootElement;
        ThrowIfApiError(userInfoJson);

        var userId = userInfoJson.TryGetProperty("UserId", out var userIdValue) ? userIdValue.GetString() : null;
        var openId = userInfoJson.TryGetProperty("OpenId", out var openIdValue) ? openIdValue.GetString() : userId;
        if (string.IsNullOrWhiteSpace(openId))
        {
            throw new InvalidOperationException("WeCom callback missing UserId/OpenId.");
        }

        var payload = JsonSerializer.Serialize(new
        {
            corpToken,
            userId,
            openId
        });

        return new ExternalUserAccessToken(
            Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)),
            DateTimeOffset.UtcNow.AddMinutes(5));
    }

    public async Task<ExternalUserProfile> GetUserProfileAsync(
        IdentityProviderEntity provider,
        ExternalUserAccessToken accessToken,
        CancellationToken cancellationToken = default)
    {
        var payload = ParsePayload(accessToken.AccessToken);

        if (!string.IsNullOrWhiteSpace(payload.UserId))
        {
            using var response = await httpClient.GetAsync(
                $"{UserDetailApi}?access_token={Uri.EscapeDataString(payload.CorpToken)}&userid={Uri.EscapeDataString(payload.UserId)}",
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonDocument.Parse(rawJson).RootElement;
            ThrowIfApiError(json);

            return new ExternalUserProfile(
                payload.OpenId,
                null,
                json.TryGetProperty("name", out var nameNode) ? nameNode.GetString() : payload.UserId,
                json.TryGetProperty("email", out var emailNode) ? emailNode.GetString() : null,
                json.TryGetProperty("mobile", out var mobileNode) ? mobileNode.GetString() : null,
                json.TryGetProperty("avatar", out var avatarNode) ? avatarNode.GetString() : null,
                rawJson);
        }

        var fallbackJson = $$"""{"openId":"{{payload.OpenId}}"}""";
        return new ExternalUserProfile(payload.OpenId, null, null, null, null, null, fallbackJson);
    }

    private async Task<string> GetCorpTokenAsync(string corpId, string appSecret, CancellationToken cancellationToken)
    {
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
                ThrowIfApiError(tokenJson);
                var token = tokenJson.GetProperty("access_token").GetString()
                    ?? throw new InvalidOperationException("WeCom corp access_token missing.");
                var expiresIn = tokenJson.TryGetProperty("expires_in", out var expiresValue) ? expiresValue.GetInt32() : 7200;
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(60, expiresIn - 120));
                return token;
            });

        return corpToken!;
    }

    private static void ThrowIfApiError(JsonElement payload)
    {
        if (payload.TryGetProperty("errcode", out var errCodeNode))
        {
            var errCode = errCodeNode.GetInt32();
            if (errCode != 0)
            {
                var errMessage = payload.TryGetProperty("errmsg", out var errMsgNode) ? errMsgNode.GetString() : "unknown";
                throw new InvalidOperationException($"WeCom API error ({errCode}): {errMessage}");
            }
        }
    }

    private static WeComTokenPayload ParsePayload(string encodedPayload)
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPayload));
        var payload = JsonSerializer.Deserialize<WeComTokenPayload>(json)
                      ?? throw new InvalidOperationException("Invalid WeCom token payload.");
        return payload;
    }

    private sealed record WeComTokenPayload(string CorpToken, string? UserId, string OpenId);
}
