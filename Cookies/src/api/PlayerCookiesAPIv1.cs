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
    protected JsonSerializerOptions jsonOptions = new() { IncludeFields = true };

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
            if (cachedCookies.TryGetValue(steamid, out var value))
            {
                value.Clear();
                if (!saveQueue.Contains(steamid))
                {
                    saveQueue.Enqueue(steamid);
                }
            }
        }
        else
        {
            var connection = core.Database.GetConnection("cookies");

            Task.Run(async () =>
            {
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

                user.Data = [];
                await connection.UpdateAsync(user);
            });
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
                    return CookieValueConverter.Convert<T>(value, jsonOptions);
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
                var userToInsert = user;

                Task.Run(async () =>
                {
                    var id = await connection.InsertAsync(userToInsert);
                    if (id is long longId)
                    {
                        userToInsert.Id = (ulong)longId;
                    }
                    else if (id is ulong ulongId)
                    {
                        userToInsert.Id = ulongId;
                    }
                    else
                    {
                        throw new Exception("Unexpected ID type returned from database.");
                    }
                });
            }

            return CookieValueConverter.Convert<T>(user.Data[key], jsonOptions);
        }
    }

    public T? GetOrDefault<T>(IPlayer player, string key, T defaultValue)
    {
        return GetOrDefault((long)player.SteamID, key, defaultValue);
    }

    public T? GetOrDefault<T>(long steamid, string key, T defaultValue)
    {
        if (cachedCookies.TryGetValue(steamid, out var data))
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

            Set(steamid, key, defaultValue);
            return defaultValue;
        }
        else
        {
            var connection = core.Database.GetConnection("cookies");

            var users = connection.Select<PlayerCookie>(u => u.SteamId64 == steamid);
            var user = users.FirstOrDefault();
            var isNewUser = user == null;

            if (isNewUser)
            {
                user = new PlayerCookie
                {
                    SteamId64 = steamid,
                    Data = []
                };
            }

            if (!isNewUser && user!.Data.TryGetValue(key, out var raw))
            {
                return CookieValueConverter.Convert<T>(raw, jsonOptions);
            }

#pragma warning disable CS8601 // Possible null reference assignment.
            user!.Data[key] = defaultValue;
#pragma warning restore CS8601 // Possible null reference assignment.

            var userToPersist = user;

            Task.Run(async () =>
            {
                if (isNewUser)
                {
                    var id = await connection.InsertAsync(userToPersist);
                    if (id is long longId)
                    {
                        userToPersist.Id = (ulong)longId;
                    }
                    else if (id is ulong ulongId)
                    {
                        userToPersist.Id = ulongId;
                    }
                    else
                    {
                        throw new Exception("Unexpected ID type returned from database.");
                    }
                }

                await connection.UpdateAsync(userToPersist);
            });

            return defaultValue;
        }
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
        if(!player.IsValid) return;
        
        var connection = core.Database.GetConnection("cookies");
        var steamid = (long)player.SteamID;

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

        cachedCookies[steamid] = user.Data;
        playerBySteamId[steamid] = player;
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
            var data = cachedCookies.GetOrAdd(steamid, static _ => new Dictionary<string, object>());

#pragma warning disable CS8601 // Possible null reference assignment.
            data[key] = value;
#pragma warning restore CS8601 // Possible null reference assignment.

            if (!saveQueue.Contains(steamid))
            {
                saveQueue.Enqueue(steamid);
            }
        }
        else
        {
            var connection = core.Database.GetConnection("cookies");

            Task.Run(async () =>
            {
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

#pragma warning disable CS8601 // Possible null reference assignment.
                user.Data[key] = value;
#pragma warning restore CS8601 // Possible null reference assignment.
                await connection.UpdateAsync(user);
            });
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

            Task.Run(async () =>
            {
                var users = await connection.SelectAsync<PlayerCookie>(u => u.SteamId64 == steamid);
                var user = users.FirstOrDefault();

                if (user != null)
                {
                    user.Data.Remove(key);
                }
            });
        }
    }
}