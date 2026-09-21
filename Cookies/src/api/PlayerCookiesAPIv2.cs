using System.Collections.Concurrent;
using Cookies.Contract;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Cookies.API;

public class PlayerCookiesAPIv2 : PlayerCookiesAPIv1, IPlayerCookiesAPIv2
{
    private ConcurrentDictionary<long, Dictionary<string, object>> sessionCookies;

    public PlayerCookiesAPIv2(
        ISwiftlyCore core,
        ref ConcurrentDictionary<long, Dictionary<string, object>> cachedCookies,
        ref ConcurrentQueue<long> saveQueue,
        ref ConcurrentDictionary<long, IPlayer> playerBySteamId,
        ref ConcurrentDictionary<long, Dictionary<string, object>> sessionCookies
    ) : base(core, ref cachedCookies, ref saveQueue, ref playerBySteamId)
    {
        this.sessionCookies = sessionCookies;
    }

    public void ClearSession(IPlayer player)
    {
        ClearSession((long)player.SteamID);
    }

    public void ClearSession(long steamid)
    {
        if (sessionCookies.TryGetValue(steamid, out var value))
        {
            value.Clear();
        }
    }

    public T? GetSession<T>(IPlayer player, string key)
    {
        return GetSession<T>((long)player.SteamID, key);
    }

    public T? GetSession<T>(long steamid, string key)
    {
        if (sessionCookies.TryGetValue(steamid, out var data))
        {
            if (data.TryGetValue(key, out var value))
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
        else return default;
    }

    public T? GetSessionOrDefault<T>(IPlayer player, string key, T defaultValue)
    {
        return GetSessionOrDefault((long)player.SteamID, key, defaultValue);
    }

    public T? GetSessionOrDefault<T>(long steamid, string key, T defaultValue)
    {
        if (!HasSession(steamid, key))
        {
            SetSession(steamid, key, defaultValue);
            return defaultValue;
        }
        else return GetSession<T>(steamid, key);
    }

    public bool HasSession(IPlayer player, string key)
    {
        return HasSession((long)player.SteamID, key);
    }

    public bool HasSession(long steamid, string key)
    {
        return sessionCookies.TryGetValue(steamid, out var userCookies) && userCookies.ContainsKey(key);
    }

    public void SetSession<T>(IPlayer player, string key, T value)
    {
        SetSession((long)player.SteamID, key, value);
    }

    public void SetSession<T>(long steamid, string key, T value)
    {
        var data = sessionCookies.GetOrAdd(steamid, static _ => new Dictionary<string, object>());

#pragma warning disable CS8601 // Possible null reference assignment.
        data[key] = value;
#pragma warning restore CS8601 // Possible null reference assignment.
    }

    public void UnsetSession(IPlayer player, string key)
    {
        UnsetSession((long)player.SteamID, key);
    }

    public void UnsetSession(long steamid, string key)
    {
        if (sessionCookies.TryGetValue(steamid, out var userCookies))
        {
            userCookies.Remove(key);
        }
    }
}