using Cookies.Contract;
using Cookies.Storage;
using SwiftlyS2.Shared.Players;

namespace Cookies.API;

public class PlayerCookiesAPIv1 : IPlayerCookiesAPIv1
{
    private readonly CookieStore store;

    internal PlayerCookiesAPIv1(CookieStore store) => this.store = store;

    public void Clear(IPlayer player) => Clear((long)player.SteamID);
    public void Clear(long steamid) => store.Clear(steamid);

    public T? Get<T>(IPlayer player, string key) => Get<T>((long)player.SteamID, key);
    public T? Get<T>(long steamid, string key) =>
        store.TryGet(steamid, key, out var value) ? CookieJson.ConvertOrDefault<T>(value) : default;

    public T? GetOrDefault<T>(IPlayer player, string key, T defaultValue) => GetOrDefault((long)player.SteamID, key, defaultValue);
    public T? GetOrDefault<T>(long steamid, string key, T defaultValue)
    {
        if (store.TryGet(steamid, key, out var value)) return CookieJson.ConvertOrDefault<T>(value);

        store.SetIfAbsent(steamid, key, CookieJson.ToElement(defaultValue));
        return defaultValue;
    }

    public bool Has(IPlayer player, string key) => Has((long)player.SteamID, key);
    public bool Has(long steamid, string key) => store.TryGet(steamid, key, out _);

    public Task Load(IPlayer player) => player.IsValid ? store.LoadAsync((long)player.SteamID) : Task.CompletedTask;

    public Task Save(IPlayer player) => player.IsValid ? Save((long)player.SteamID) : Task.CompletedTask;
    public Task Save(long steamid) => store.SaveAsync(steamid);

    public void Set<T>(IPlayer player, string key, T value) => Set((long)player.SteamID, key, value);
    // Serialized now, so a caller mutating its object later can't race the background save.
    public void Set<T>(long steamid, string key, T value) => store.Set(steamid, key, CookieJson.ToElement(value));

    public void Unset(IPlayer player, string key) => Unset((long)player.SteamID, key);
    public void Unset(long steamid, string key) => store.Unset(steamid, key);
}
