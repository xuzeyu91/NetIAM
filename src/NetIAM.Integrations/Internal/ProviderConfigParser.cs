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

    public static bool OptionalBoolean(JsonElement config, string propertyName, bool fallback = false)
    {
        if (!config.TryGetProperty(propertyName, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => fallback
        };
    }

    public static int OptionalInt(JsonElement config, string propertyName, int fallback = 0)
    {
        if (!config.TryGetProperty(propertyName, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var parsed) => parsed,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => fallback
        };
    }
}
