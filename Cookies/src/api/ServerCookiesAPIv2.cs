using System.Collections.Concurrent;
using System.Text.Json;
using Cookies.Contract;
using Cookies.Database.Models;
using Dommel;
using SwiftlyS2.Shared;

namespace Cookies.API;

public class ServerCookiesAPIv2 : ServerCookiesAPIv1, IServerCookiesAPIv2
{
    private ConcurrentDictionary<long, Dictionary<string, object>> sessionCookies;

    public ServerCookiesAPIv2(
        ISwiftlyCore core,
        ref ConcurrentDictionary<long, Dictionary<string, object>> cachedCookies,
        ref ConcurrentQueue<long> saveQueue,
        ref ConcurrentDictionary<long, Dictionary<string, object>> sessionCookies
    ) : base(core, ref cachedCookies, ref saveQueue)
    {
        this.sessionCookies = sessionCookies;
    }

    public void ClearSession()
    {
        if (sessionCookies.TryGetValue(-1, out var value))
        {
            value.Clear();
        }
    }

    public T? GetSession<T>(string key)
    {
        if (sessionCookies.TryGetValue(-1, out var userCookies) && userCookies.TryGetValue(key, out var value))
        {
            try
            {
                if (value is JsonElement element)
                {
                    return JsonSerializer.Deserialize<T>(element.GetRawText(), jsonOptions);
                }
                else if (value is T typedValue)
                {
                    return typedValue;
                }
                else
                {
                    string json = JsonSerializer.Serialize(value);
                    return JsonSerializer.Deserialize<T>(json, jsonOptions);
                }
            }
            catch (Exception)
            {
                return default;
            }
        }
        return default;
    }

    public T? GetSessionOrDefault<T>(string key, T defaultValue)
    {
        if (!HasSession(key))
        {
            SetSession(key, defaultValue);
            return defaultValue;
        }
        else return GetSession<T>(key);
    }

    public bool HasSession(string key)
    {
        return sessionCookies.TryGetValue(-1, out var userCookies) && userCookies.ContainsKey(key);
    }

    public void SetSession<T>(string key, T value)
    {
        if (!sessionCookies.ContainsKey(-1))
        {
            sessionCookies[-1] = [];
        }

#pragma warning disable CS8601 // Possible null reference assignment.
        sessionCookies[-1][key] = value;
#pragma warning restore CS8601 // Possible null reference assignment.
    }

    public void UnsetSession(string key)
    {
        if (sessionCookies.TryGetValue(-1, out var userCookies))
        {
            userCookies.Remove(key);
        }
    }
}