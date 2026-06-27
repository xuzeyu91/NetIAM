using System.Text.Json;
using NetIAM.Domain.Entities;

namespace NetIAM.Integrations.Internal;

internal static class ProviderConfigParser
{
    public static JsonElement ParseIdentityProviderConfig(IdentityProviderEntity provider)
    {
        return JsonDocument.Parse(provider.ConfigJson).RootElement.Clone();
    }

    public static JsonElement ParseIdentitySourceConfig(IdentitySourceEntity identitySource)
    {
        return JsonDocument.Parse(identitySource.BasicConfigJson).RootElement.Clone();
    }

    public static string RequiredString(JsonElement config, string propertyName)
    {
        if (!config.TryGetProperty(propertyName, out var value) || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException($"Missing required provider config: {propertyName}");
        }

        return value.GetString()!;
    }

    public static string OptionalString(JsonElement config, string propertyName, string fallback = "")
    {
        return config.TryGetProperty(propertyName, out var value) && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : fallback;
    }
}
