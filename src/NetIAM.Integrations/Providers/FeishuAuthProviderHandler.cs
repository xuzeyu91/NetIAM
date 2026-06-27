using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using NetIAM.Domain.Contracts;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Integrations.Abstractions;
using NetIAM.Integrations.Internal;

namespace NetIAM.Integrations.Providers;

public sealed class FeishuAuthProviderHandler(HttpClient httpClient) : IExternalAuthProviderHandler
{
    public ExternalProviderType ProviderType => ExternalProviderType.Feishu;

    public Task<string> BuildAuthorizeUrlAsync(
        IdentityProviderEntity provider,
        ExternalAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var config = ProviderConfigParser.ParseIdentityProviderConfig(provider);
        var clientId = ProviderConfigParser.RequiredStringAny(config, "clientId", "appId");
        var callbackUri = $"{request.CallbackBaseUri.TrimEnd('/')}/login/feishu/{provider.Code}";
        var redirectUri = UrlEncoder.Default.Encode(callbackUri);
        var state = UrlEncoder.Default.Encode(request.State);
        var authorizeUrl =
            $"https://passport.feishu.cn/suite/passport/oauth/authorize?client_id={clientId}&redirect_uri={redirectUri}&response_type=code&scope=contact:user.base:readonly&state={state}";

        return Task.FromResult(authorizeUrl);
    }

    public async Task<ExternalUserAccessToken> ExchangeTokenAsync(
        IdentityProviderEntity provider,
        ExternalAuthCallback callback,
        CancellationToken cancellationToken = default)
    {
        var config = ProviderConfigParser.ParseIdentityProviderConfig(provider);
        var clientId = ProviderConfigParser.RequiredStringAny(config, "clientId", "appId");
        var clientSecret = ProviderConfigParser.RequiredStringAny(config, "clientSecret", "appSecret");
        var redirectUri = ProviderConfigParser.OptionalString(config, "redirectUri");
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            redirectUri = callback.RedirectUri;
        }

        var payload = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = callback.AuthorizationCode
        };

        if (!string.IsNullOrWhiteSpace(redirectUri))
        {
            payload["redirect_uri"] = redirectUri;
        }

        using var response = await httpClient.SendAsyncWithGovernance(
            "feishu-auth",
            () => new HttpRequestMessage(HttpMethod.Post, "https://passport.feishu.cn/suite/passport/oauth/token")
            {
                Content = new FormUrlEncodedContent(payload)
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement;
        var token = json.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Feishu access_token missing.");
        var expiresIn = json.TryGetProperty("expires_in", out var expiresValue) ? expiresValue.GetInt32() : 7200;
        return new ExternalUserAccessToken(token, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
    }

    public async Task<ExternalUserProfile> GetUserProfileAsync(
        IdentityProviderEntity provider,
        ExternalUserAccessToken accessToken,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.SendAsyncWithGovernance(
            "feishu-auth",
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://passport.feishu.cn/suite/passport/oauth/userinfo");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.AccessToken);
                return request;
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(rawJson).RootElement;

        var openId = json.TryGetProperty("open_id", out var openIdValue)
            ? openIdValue.GetString()
            : json.TryGetProperty("sub", out var subValue) ? subValue.GetString() : null;

        if (string.IsNullOrWhiteSpace(openId))
        {
            throw new InvalidOperationException("Feishu user profile missing open_id.");
        }

        return new ExternalUserProfile(
            openId,
            json.TryGetProperty("union_id", out var unionId) ? unionId.GetString() : null,
            json.TryGetProperty("name", out var name) ? name.GetString() : null,
            json.TryGetProperty("email", out var email) ? email.GetString() : null,
            json.TryGetProperty("mobile", out var mobile) ? mobile.GetString() : null,
            json.TryGetProperty("avatar_url", out var avatar) ? avatar.GetString() : null,
            rawJson);
    }
}
