using System.Collections.Concurrent;

namespace Cookies.Storage;

/// <summary>Values that are never saved: a player's last until they disconnect, the server's until unload.</summary>
internal sealed class SessionStore
{
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, object?>> sessions = new();

    public T? Get<T>(long steamId, string key) =>
        sessions.TryGetValue(steamId, out var values) && values.TryGetValue(key, out var value)
            ? CookieJson.ConvertOrDefault<T>(value)
            : default;

    public T? GetOrAdd<T>(long steamId, string key, T defaultValue) =>
        sessions.GetOrAdd(steamId, static _ => new()).TryAdd(key, defaultValue) ? defaultValue : Get<T>(steamId, key);

    public bool Has(long steamId, string key) =>
        sessions.TryGetValue(steamId, out var values) && values.ContainsKey(key);

    public void Set<T>(long steamId, string key, T value) =>
        sessions.GetOrAdd(steamId, static _ => new())[key] = value;

    public void Unset(long steamId, string key)
    {
        if (sessions.TryGetValue(steamId, out var values)) values.TryRemove(key, out _);
    }

    public void Clear(long steamId) => sessions.TryRemove(steamId, out _);
}
