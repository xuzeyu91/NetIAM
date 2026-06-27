using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using NetIAM.Domain.Contracts;
using NetIAM.Domain.Entities;
using NetIAM.Domain.Enums;
using NetIAM.Integrations.Abstractions;
using NetIAM.Integrations.Internal;

namespace NetIAM.Integrations.Providers;

public sealed class DingTalkAuthProviderHandler(HttpClient httpClient) : IExternalAuthProviderHandler
{
    public ExternalProviderType ProviderType => ExternalProviderType.DingTalk;

    public Task<string> BuildAuthorizeUrlAsync(
        IdentityProviderEntity provider,
        ExternalAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var config = ProviderConfigParser.ParseIdentityProviderConfig(provider);
        var appKey = ProviderConfigParser.RequiredStringAny(config, "appKey", "appId", "clientId");
        var corpId = ProviderConfigParser.OptionalString(config, "corpId");
        var redirectUri = UrlEncoder.Default.Encode($"{request.CallbackBaseUri.TrimEnd('/')}/login/dingtalk/{provider.Code}");
        var state = UrlEncoder.Default.Encode(request.State);
        var scope = UrlEncoder.Default.Encode(string.IsNullOrWhiteSpace(corpId) ? "openid" : "openid corpid");

        var authorizeUrl = $"https://login.dingtalk.com/oauth2/auth?redirect_uri={redirectUri}&response_type=code&client_id={appKey}&scope={scope}&state={state}&prompt=consent";
        if (!string.IsNullOrWhiteSpace(corpId))
        {
            authorizeUrl += $"&corpId={UrlEncoder.Default.Encode(corpId)}";
        }

        return Task.FromResult(authorizeUrl);
    }

    public async Task<ExternalUserAccessToken> ExchangeTokenAsync(
        IdentityProviderEntity provider,
        ExternalAuthCallback callback,
        CancellationToken cancellationToken = default)
    {
        var config = ProviderConfigParser.ParseIdentityProviderConfig(provider);
        var appKey = ProviderConfigParser.RequiredStringAny(config, "appKey", "appId", "clientId");
        var appSecret = ProviderConfigParser.RequiredStringAny(config, "appSecret", "clientSecret");

        var payload = new
        {
            clientId = appKey,
            clientSecret = appSecret,
            code = callback.AuthorizationCode,
            grantType = "authorization_code"
        };

        using var response = await httpClient.PostJsonAsyncWithGovernance(
            "dingtalk-auth",
            "https://api.dingtalk.com/v1.0/oauth2/userAccessToken",
            payload,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken)).RootElement;
        var token = json.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("DingTalk accessToken missing.");
        var expiresIn = json.TryGetProperty("expireIn", out var expiresValue) ? expiresValue.GetInt32() : 7200;
        return new ExternalUserAccessToken(token, DateTimeOffset.UtcNow.AddSeconds(expiresIn));
    }

    public async Task<ExternalUserProfile> GetUserProfileAsync(
        IdentityProviderEntity provider,
        ExternalUserAccessToken accessToken,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.SendAsyncWithGovernance(
            "dingtalk-auth",
            () =>
            {
                var governedRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.dingtalk.com/v1.0/contact/users/me");
                governedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.AccessToken);
                return governedRequest;
            },
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonDocument.Parse(rawJson).RootElement;

        var openId = json.TryGetProperty("openId", out var openIdValue)
            ? openIdValue.GetString()
            : json.TryGetProperty("unionId", out var unionIdFallback) ? unionIdFallback.GetString() : null;

        if (string.IsNullOrWhiteSpace(openId))
        {
            throw new InvalidOperationException("DingTalk user profile missing openId.");
        }

        return new ExternalUserProfile(
            openId,
            json.TryGetProperty("unionId", out var unionId) ? unionId.GetString() : null,
            json.TryGetProperty("nick", out var nick) ? nick.GetString() : null,
            json.TryGetProperty("email", out var email) ? email.GetString() : null,
            json.TryGetProperty("mobile", out var mobile) ? mobile.GetString() : null,
            json.TryGetProperty("avatarUrl", out var avatar) ? avatar.GetString() : null,
            rawJson);
    }
}
