using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocAgent.Infrastructure;

/// <summary>One System.Text.Json setup for every jsonb column: camelCase names, enums as text.</summary>
public static class DocAgentJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new JsonException($"The stored JSON for {typeof(T).Name} is null.");
}
