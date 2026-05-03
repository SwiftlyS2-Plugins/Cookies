using System.Collections.Concurrent;
using System.Text.Json;
using Cookies.Contract;
using Cookies.Database.Models;
using Dommel;
using SwiftlyS2.Shared;

namespace Cookies.API;

public class ServerCookiesAPIv1 : IServerCookiesAPIv1
{
    private ISwiftlyCore Core;
    private ConcurrentDictionary<long, Dictionary<string, object>> CachedCookies;
    private ConcurrentQueue<long> SaveQueue;

    public ServerCookiesAPIv1(ISwiftlyCore core, ref ConcurrentDictionary<long, Dictionary<string, object>> cachedCookies, ref ConcurrentQueue<long> saveQueue)
    {
        Core = core;
        CachedCookies = cachedCookies;
        SaveQueue = saveQueue;
    }

    public void Clear()
    {
        if (CachedCookies.TryGetValue(-1, out Dictionary<string, object>? value))
        {
            value.Clear();
            if (!SaveQueue.Contains(-1))
            {
                SaveQueue.Enqueue(-1);
            }
        }
    }

    public T? Get<T>(string key)
    {
        if (CachedCookies.TryGetValue(-1, out var userCookies) && userCookies.TryGetValue(key, out var value))
        {
            try
            {
                if (value is JsonElement element)
                {
                    return JsonSerializer.Deserialize<T>(element.GetRawText(), new JsonSerializerOptions { IncludeFields = true });
                }
                else if (value is T typedValue)
                {
                    return typedValue;
                }
                else
                {
                    string json = JsonSerializer.Serialize(value);
                    return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { IncludeFields = true });
                }
            }
            catch (Exception)
            {
                return default;
            }
        }
        return default;
    }

    public T? GetOrDefault<T>(string key, T defaultValue)
    {
        if (!Has(key))
        {
            Set(key, defaultValue);
            return defaultValue;
        }
        else return Get<T>(key);
    }

    public bool Has(string key)
    {
        return CachedCookies.ContainsKey(-1) && CachedCookies[-1].ContainsKey(key);
    }

    public async Task Load()
    {
        var connection = Core.Database.GetConnection("cookies");

        var users = await connection.SelectAsync<PlayerCookie>(u => u.SteamId64 == -1);
        var user = users.FirstOrDefault();

        if (user == null)
        {
            user = new PlayerCookie
            {
                SteamId64 = -1,
                Data = []
            };
            var id = await connection.InsertAsync(user);
            if (id is long longId)
            {
                user.Id = (ulong)longId;
            }
            else if (id is ulong ulongId)
            {
                user.Id = ulongId;
            }
            else
            {
                throw new Exception("Unexpected ID type returned from database.");
            }
        }

        CachedCookies[-1] = user.Data;
    }

    public async Task Save()
    {
        var connection = Core.Database.GetConnection("cookies");

        var users = await connection.SelectAsync<PlayerCookie>(u => u.SteamId64 == -1);
        var user = users.FirstOrDefault();

        if (user == null)
        {
            user = new PlayerCookie
            {
                SteamId64 = -1,
                Data = []
            };
            var id = await connection.InsertAsync(user);
            if (id is long longId)
            {
                user.Id = (ulong)longId;
            }
            else if (id is ulong ulongId)
            {
                user.Id = ulongId;
            }
            else
            {
                throw new Exception("Unexpected ID type returned from database.");
            }
        }

        if (CachedCookies.TryGetValue(-1, out var data))
        {
            user.Data = data;
        }

        await connection.UpdateAsync(user);
    }

    public void Set<T>(string key, T value)
    {
        if (!CachedCookies.ContainsKey(-1))
        {
            CachedCookies[-1] = new Dictionary<string, object>();
        }

#pragma warning disable CS8601 // Possible null reference assignment.
        CachedCookies[-1][key] = value;
#pragma warning restore CS8601 // Possible null reference assignment.
        if (!SaveQueue.Contains(-1))
        {
            SaveQueue.Enqueue(-1);
        }
    }

    public void Unset(string key)
    {
        if (CachedCookies.ContainsKey(-1) && CachedCookies[-1].ContainsKey(key))
        {
            CachedCookies[-1].Remove(key);
            if (!SaveQueue.Contains(-1))
            {
                SaveQueue.Enqueue(-1);
            }
        }
    }
}