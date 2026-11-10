using Cookies.Contract;
using Cookies.Storage;

namespace Cookies.API;

public class ServerCookiesAPIv1 : IServerCookiesAPIv1
{
    private readonly CookieStore store;

    internal ServerCookiesAPIv1(CookieStore store) => this.store = store;

    public void Clear() => store.Clear(CookieStore.ServerId);

    public T? Get<T>(string key) =>
        store.TryGet(CookieStore.ServerId, key, out var value) ? CookieJson.ConvertOrDefault<T>(value) : default;

    public T? GetOrDefault<T>(string key, T defaultValue)
    {
        if (store.TryGet(CookieStore.ServerId, key, out var value)) return CookieJson.ConvertOrDefault<T>(value);

        store.SetIfAbsent(CookieStore.ServerId, key, CookieJson.ToElement(defaultValue));
        return defaultValue;
    }

    public bool Has(string key) => store.TryGet(CookieStore.ServerId, key, out _);

    public Task Load() => store.LoadAsync(CookieStore.ServerId);

    public Task Save() => store.SaveAsync(CookieStore.ServerId);

    public void Set<T>(string key, T value) => store.Set(CookieStore.ServerId, key, CookieJson.ToElement(value));

    public void Unset(string key) => store.Unset(CookieStore.ServerId, key);
}
