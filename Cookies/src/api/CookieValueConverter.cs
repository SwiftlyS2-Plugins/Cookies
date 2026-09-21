using System.Text.Json;

namespace Cookies.API;

internal static class CookieValueConverter
{
    public static T? Convert<T>(object value, JsonSerializerOptions options)
    {
        if (value is JsonElement element)
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText(), options);
        }
        else if (value is T typedValue)
        {
            return typedValue;
        }
        else
        {
            string json = JsonSerializer.Serialize(value);
            return JsonSerializer.Deserialize<T>(json, options);
        }
    }
}
