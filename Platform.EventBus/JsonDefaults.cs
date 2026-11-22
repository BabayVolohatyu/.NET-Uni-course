using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.EventBus;

/// <summary>
/// One JSON contract for every publisher and consumer: camelCase, ISO-8601 dates, enum strings.
/// </summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
