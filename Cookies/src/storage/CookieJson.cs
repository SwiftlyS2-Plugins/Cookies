using System.Text.Json;

namespace Cookies.Storage;

internal static class CookieJson
{
    public static readonly JsonSerializerOptions Options = new() { IncludeFields = true };

    public static JsonElement ToElement<T>(T value) => JsonSerializer.SerializeToElement(value, Options);

    public static T? ConvertOrDefault<T>(JsonElement value)
    {
        try
        {
            return value.Deserialize<T>(Options);
        }
        catch (Exception)
        {
            return default;
        }
    }

    public static T? ConvertOrDefault<T>(object? value)
    {
        try
        {
            return value switch
            {
                JsonElement element => element.Deserialize<T>(Options),
                T typed => typed,
                _ => JsonSerializer.SerializeToElement(value, Options).Deserialize<T>(Options),
            };
        }
        catch (Exception)
        {
            return default;
        }
    }

    /// <exception cref="JsonException">The stored value is not a JSON object.</exception>
    public static Dictionary<string, JsonElement> Parse(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, JsonElement>()
            : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, Options) ?? new Dictionary<string, JsonElement>();

    public static string Serialize(Dictionary<string, JsonElement> values) => JsonSerializer.Serialize(values, Options);
}
