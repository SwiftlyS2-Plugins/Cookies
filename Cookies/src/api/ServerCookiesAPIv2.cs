using Cookies.Contract;
using Cookies.Storage;

namespace Cookies.API;

public class ServerCookiesAPIv2 : ServerCookiesAPIv1, IServerCookiesAPIv2
{
    private readonly SessionStore sessions;

    internal ServerCookiesAPIv2(CookieStore store, SessionStore sessions) : base(store) => this.sessions = sessions;

    public void ClearSession() => sessions.Clear(CookieStore.ServerId);

    public T? GetSession<T>(string key) => sessions.Get<T>(CookieStore.ServerId, key);

    public T? GetSessionOrDefault<T>(string key, T defaultValue) => sessions.GetOrAdd(CookieStore.ServerId, key, defaultValue);

    public bool HasSession(string key) => sessions.Has(CookieStore.ServerId, key);

    public void SetSession<T>(string key, T value) => sessions.Set(CookieStore.ServerId, key, value);

    public void UnsetSession(string key) => sessions.Unset(CookieStore.ServerId, key);
}
