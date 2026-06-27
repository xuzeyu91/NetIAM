using System.Net.Http.Headers;
using System.Net.Http.Json;
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
        var appKey = ProviderConfigParser.RequiredString(config, "appKey");
        var redirectUri = UrlEncoder.Default.Encode($"{request.CallbackBaseUri.TrimEnd('/')}/login/dingtalk/{provider.Code}");
        var state = UrlEncoder.Default.Encode(request.State);

        var authorizeUrl =
            $"https://login.dingtalk.com/oauth2/auth?redirect_uri={redirectUri}&response_type=code&client_id={appKey}&scope=openid&state={state}&prompt=consent";
        return Task.FromResult(authorizeUrl);
    }

    public async Task<ExternalUserAccessToken> ExchangeTokenAsync(
        IdentityProviderEntity provider,
        ExternalAuthCallback callback,
        CancellationToken cancellationToken = default)
    {
        var config = ProviderConfigParser.ParseIdentityProviderConfig(provider);
        var appKey = ProviderConfigParser.RequiredString(config, "appKey");
        var appSecret = ProviderConfigParser.RequiredString(config, "appSecret");

        var payload = new
        {
            clientId = appKey,
            clientSecret = appSecret,
            code = callback.AuthorizationCode,
            grantType = "authorization_code"
        };

        using var response = await httpClient.PostAsJsonAsync(
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
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.dingtalk.com/v1.0/contact/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.AccessToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
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
