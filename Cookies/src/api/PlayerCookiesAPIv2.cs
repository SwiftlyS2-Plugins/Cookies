using Cookies.Contract;
using Cookies.Storage;
using SwiftlyS2.Shared.Players;

namespace Cookies.API;

public class PlayerCookiesAPIv2 : PlayerCookiesAPIv1, IPlayerCookiesAPIv2
{
    private readonly SessionStore sessions;

    internal PlayerCookiesAPIv2(CookieStore store, SessionStore sessions) : base(store) => this.sessions = sessions;

    public void ClearSession(IPlayer player) => ClearSession((long)player.SteamID);
    public void ClearSession(long steamid) => sessions.Clear(steamid);

    public T? GetSession<T>(IPlayer player, string key) => GetSession<T>((long)player.SteamID, key);
    public T? GetSession<T>(long steamid, string key) => sessions.Get<T>(steamid, key);

    public T? GetSessionOrDefault<T>(IPlayer player, string key, T defaultValue) =>
        GetSessionOrDefault((long)player.SteamID, key, defaultValue);
    public T? GetSessionOrDefault<T>(long steamid, string key, T defaultValue) => sessions.GetOrAdd(steamid, key, defaultValue);

    public bool HasSession(IPlayer player, string key) => HasSession((long)player.SteamID, key);
    public bool HasSession(long steamid, string key) => sessions.Has(steamid, key);

    public void SetSession<T>(IPlayer player, string key, T value) => SetSession((long)player.SteamID, key, value);
    public void SetSession<T>(long steamid, string key, T value) => sessions.Set(steamid, key, value);

    public void UnsetSession(IPlayer player, string key) => UnsetSession((long)player.SteamID, key);
    public void UnsetSession(long steamid, string key) => sessions.Unset(steamid, key);
}
