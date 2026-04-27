using System.Collections.Concurrent;
using System.Text.Json;
using Cookies.Contract;
using Cookies.Database.Models;
using Dommel;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace Cookies.API;

public class PlayerCookiesAPIv1 : IPlayerCookiesAPIv1
{
    private ISwiftlyCore core;
    private ConcurrentDictionary<long, Dictionary<string, object>> cachedCookies;
    private ConcurrentQueue<long> saveQueue;
    private ConcurrentDictionary<long, IPlayer> playerBySteamId;

    public PlayerCookiesAPIv1(ISwiftlyCore core, ref ConcurrentDictionary<long, Dictionary<string, object>> cachedCookies, ref ConcurrentQueue<long> saveQueue, ref ConcurrentDictionary<long, IPlayer> playerBySteamId)
    {
        this.core = core;
        this.cachedCookies = cachedCookies;
        this.saveQueue = saveQueue;
        this.playerBySteamId = playerBySteamId;
    }

    public void Clear(IPlayer player)
    {
        Clear((long)player.SteamID);
    }

    public void Clear(long steamid)
    {
        if (playerBySteamId.ContainsKey(steamid))
        {
            if (cachedCookies.ContainsKey(steamid))
            {
                cachedCookies[steamid].Clear();
                if (!saveQueue.Contains(steamid))
                {
                    saveQueue.Enqueue(steamid);
                }
            }
        }
        else
        {
            var connection = core.Database.GetConnection("cookies");

            var users = connection.Select<PlayerCookie>(u => u.SteamId64 == steamid);
            var user = users.FirstOrDefault();

            if (user == null)
            {
                user = new PlayerCookie
                {
                    SteamId64 = steamid,
                    Data = []
                };
                var id = connection.Insert(user);
                user.Id = (ulong)(long)id;
            }

            user.Data = [];
            connection.Update(user);
        }
    }

    public T? Get<T>(IPlayer player, string key)
    {
        return Get<T>((long)player.SteamID, key);
    }

    public T? Get<T>(long steamid, string key)
    {
        if (cachedCookies.TryGetValue(steamid, out var data))
        {
            if (data.TryGetValue(key, out var value))
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
        else
        {
            var connection = core.Database.GetConnection("cookies");

            var users = connection.Select<PlayerCookie>(u => u.SteamId64 == steamid);
            var user = users.FirstOrDefault();

            if (user == null)
            {
                user = new PlayerCookie
                {
                    SteamId64 = steamid,
                    Data = []
                };
                var id = connection.Insert(user);
                user.Id = (ulong)(long)id;
            }

            if (user.Data[key] is JsonElement element)
            {
                return JsonSerializer.Deserialize<T>(element.GetRawText(), new JsonSerializerOptions { IncludeFields = true });
            }
            else if (user.Data[key] is T typedValue)
            {
                return typedValue;
            }
            else
            {
                string json = JsonSerializer.Serialize(user.Data[key]);
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { IncludeFields = true });
            }
        }
    }

    public T? GetOrDefault<T>(IPlayer player, string key, T defaultValue)
    {
        return GetOrDefault((long)player.SteamID, key, defaultValue);
    }

    public T? GetOrDefault<T>(long steamid, string key, T defaultValue)
    {
        if (!Has(steamid, key))
        {
            Set(steamid, key, defaultValue);
            return defaultValue;
        }
        else return Get<T>(steamid, key);
    }

    public bool Has(IPlayer player, string key)
    {
        return Has((long)player.SteamID, key);
    }

    public bool Has(long steamid, string key)
    {
        if (cachedCookies.TryGetValue(steamid, out var data))
        {
            return data.ContainsKey(key);
        }
        else
        {
            var connection = core.Database.GetConnection("cookies");

            var users = connection.Select<PlayerCookie>(u => u.SteamId64 == steamid);
            var user = users.FirstOrDefault();

            if (user == null)
            {
                return false;
            }

            return user.Data.ContainsKey(key);
        }
    }

    public async Task Load(IPlayer player)
    {
        var connection = core.Database.GetConnection("cookies");

        var users = await connection.SelectAsync<PlayerCookie>(u => u.SteamId64 == (long)player.SteamID);
        var user = users.FirstOrDefault();

        if (user == null)
        {
            user = new PlayerCookie
            {
                SteamId64 = (long)player.SteamID,
                Data = []
            };
            var id = await connection.InsertAsync(user);
            user.Id = (ulong)id;
        }

        cachedCookies[(long)player.SteamID] = user.Data;
        playerBySteamId[(long)player.SteamID] = player;
    }

    public async Task Save(IPlayer player)
    {
        if (!player.IsValid) return;

        await Save((long)player.SteamID);
    }

    public async Task Save(long steamid)
    {
        var connection = core.Database.GetConnection("cookies");

        var users = await connection.SelectAsync<PlayerCookie>(u => u.SteamId64 == steamid);
        var user = users.FirstOrDefault();

        if (user == null)
        {
            user = new PlayerCookie
            {
                SteamId64 = steamid,
                Data = []
            };
            var id = await connection.InsertAsync(user);
            user.Id = (ulong)id;
        }

        if (cachedCookies.TryGetValue(steamid, out var data))
        {
            user.Data = data;
        }

        await connection.UpdateAsync(user);
    }

    public void Set<T>(IPlayer player, string key, T value)
    {
        Set((long)player.SteamID, key, value);
    }

    public void Set<T>(long steamid, string key, T value)
    {
        if (playerBySteamId.ContainsKey(steamid))
        {
            if (!cachedCookies.ContainsKey(steamid))
            {
                cachedCookies[steamid] = new Dictionary<string, object>();
            }

#pragma warning disable CS8601 // Possible null reference assignment.
            cachedCookies[steamid][key] = value;
#pragma warning restore CS8601 // Possible null reference assignment.

            if (!saveQueue.Contains(steamid))
            {
                saveQueue.Enqueue(steamid);
            }
        }
        else
        {
            var connection = core.Database.GetConnection("cookies");

            var users = connection.Select<PlayerCookie>(u => u.SteamId64 == steamid);
            var user = users.FirstOrDefault();

            if (user == null)
            {
                user = new PlayerCookie
                {
                    SteamId64 = steamid,
                    Data = []
                };
                var id = connection.Insert(user);
                user.Id = (ulong)id;
            }

#pragma warning disable CS8601 // Possible null reference assignment.
            user.Data[key] = value;
#pragma warning restore CS8601 // Possible null reference assignment.
            connection.Update(user);
        }
    }

    public void Unset(IPlayer player, string key)
    {
        Unset((long)player.SteamID, key);
    }

    public void Unset(long steamid, string key)
    {
        if (cachedCookies.TryGetValue(steamid, out var data))
        {
            data.Remove(key);
            if (!saveQueue.Contains(steamid))
            {
                saveQueue.Enqueue(steamid);
            }
        }
        else
        {
            var connection = core.Database.GetConnection("cookies");

            var users = connection.Select<PlayerCookie>(u => u.SteamId64 == steamid);
            var user = users.FirstOrDefault();

            if (user != null)
            {
                user.Data.Remove(key);
            }
        }
    }
}