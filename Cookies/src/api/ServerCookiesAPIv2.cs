using System.Collections.Concurrent;
using Cookies.Contract;
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
                return CookieValueConverter.Convert<T>(value, jsonOptions);
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
        var data = sessionCookies.GetOrAdd(-1, static _ => new Dictionary<string, object>());

#pragma warning disable CS8601 // Possible null reference assignment.
        data[key] = value;
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