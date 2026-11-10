<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" alt="Cookies logo" />
  <h1><strong>Cookies</strong></h1>
  <p>Saved player and server values for CS2 plugins, shared by every server on one database.</p>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <a href="https://github.com/SwiftlyS2-Plugins/Cookies/releases/latest"><img src="https://img.shields.io/github/v/release/SwiftlyS2-Plugins/Cookies?style=flat&logo=github&cacheSeconds=3600" alt="Latest Release"></a>
  <img src="https://img.shields.io/github/downloads/SwiftlyS2-Plugins/Cookies/total?style=flat&logo=github&cacheSeconds=3600" alt="Downloads">
  <img src="https://img.shields.io/github/stars/SwiftlyS2-Plugins/Cookies?style=flat&logo=github&cacheSeconds=3600" alt="Stars">
</p>

> [!NOTE]
> **Upgrading from 1.0.14 or earlier?**<br>
> The first start deletes duplicate `PlayerCookies` rows. Back up the table first — [Upgrading](#upgrading).

---

## ✨ Features

Cookies stores values for other plugins. It has no commands, menus or config file of its own.

- 🍪 **Player cookies** — values per SteamID, for players on the server or not
- 🖥️ **Server cookies** — one set of values shared by every server on the database
- ⏱️ **Session values** — per player or server, kept in memory and never saved
- ⚡ **Lookups from memory** — a player's cookies load once, when they join
- 💾 **Saved as they change** — key by key, so servers and websites don't overwrite each other
- 🔄 **Kept current** — changes made elsewhere show up within 5 seconds
- 🗄️ **MySQL, MariaDB, PostgreSQL or SQLite** — one `cookies` connection
- 🧩 **Shared API** — `v1` and `v2` interfaces for other plugins

---

## 🚀 Installation

### Requirements

- SwiftlyS2
- A database connection named `cookies` in SwiftlyS2's `configs/database.jsonc` — MySQL, MariaDB, PostgreSQL or SQLite. Without one, Cookies uses the default connection

### Steps

1. Unzip the release into `addons/swiftlys2/plugins/`.
2. Add the `cookies` connection to `configs/database.jsonc`, or leave Cookies on the default one.
3. Restart the server. The first start creates the `PlayerCookies` table and its index, so the database user needs `CREATE` and `INDEX` rights.

If Cookies can't reach the database at startup, it logs `[Cookies] Failed to initialize database migrations.` On a first install, nothing is stored until Cookies restarts with the database reachable.

### Where things are

| What | Path |
|------|------|
| Plugin | `addons/swiftlys2/plugins/Cookies/` |
| API contract | `addons/swiftlys2/plugins/Cookies/resources/exports/Cookies.Contract.dll` — [Using the API](#-using-the-api) |
| Connection | `cookies` in `addons/swiftlys2/configs/database.jsonc` |
| Tables | `PlayerCookies` and `VersionInfo`, created on first start — [Database](#-database) |

### Verify

| Check | Expect |
|-------|--------|
| Server console, first start | No `[Cookies]` errors |
| The `cookies` database | A `PlayerCookies` table |
| A plugin that uses Cookies | Its values survive a map change and a reconnect |

### Upgrading

From 1.0.14 or earlier:

1. Back up the `PlayerCookies` table.
2. Replace the plugin and restart. The first start keeps the row with the lowest `Id` for each SteamID, deletes the others, and adds a unique index on `SteamId64`.

---

## 💾 Database

Cookies keeps everything in one table, `PlayerCookies`, in the `cookies` connection's database. Servers on that database share it, so a player's cookies follow them from server to server.

| Column | Holds |
|--------|-------|
| `Id` | The row number |
| `SteamId64` | The player's SteamID64, or `-1` for server cookies. One row per SteamID |
| `Data` | Every value for that SteamID, as one JSON object |

`VersionInfo` lists the migrations applied.

### Loading and saving

| When | What happens |
|------|--------------|
| A player joins | Their cookies load |
| The plugin loads | Server cookies load, and those of the players already on the server |
| A lookup comes before the load finishes | It waits, up to 5 seconds. While the database is failing or hung, lookups don't wait |
| A value changes | Saved at once, key by key. While a player's values keep changing, at most once a second |
| Every 5 seconds | Every cached row is re-read, so changes made elsewhere show up |
| A player leaves | Their unsaved changes are saved and their session values cleared |
| The plugin unloads | Unsaved changes are saved, for up to 10 seconds |
| A lookup by SteamID for a player who isn't on the server | Cached too, and dropped after 2 minutes unused |
| A save fails | Retried after 5, 10, 20 and 40 seconds, then every minute |
| A bot | Shares one entry with every bot, kept in memory only |
| `SteamAuth.Mode` in SwiftlyS2's `core.jsonc` is `strict`, and Steam hasn't authorised a player yet | They share the bots' entry. Their own cookies load once Steam authorises them |

### Size limit

| Database | `Data` holds |
|----------|--------------|
| MySQL, MariaDB | 65 535 bytes |
| PostgreSQL | 16 384 characters |
| SQLite | No limit |

A save that would go over leaves out that save's largest new values and logs their keys. The player's other changes are saved.

### Several servers

| Goal | Setup |
|------|-------|
| A player's cookies on every server | One `cookies` database for all of them |
| Separate cookies per server | A different `cookies` connection on each server |

Server cookies are one set per database, so servers that share a database share them.

### Other writers

A website or tool that edits `PlayerCookies` must change single keys: lock the row with `SELECT … FOR UPDATE`, change the keys and write it back, or use one statement such as MySQL's `JSON_SET`. Writing back a whole `Data` value read earlier overwrites keys a server saved in between. Servers pick up the change within 5 seconds.

---

## 📚 API reference

Interfaces in `Cookies.Contract`, namespace `Cookies.Contract`. Referencing the contract and getting an interface: [Using the API](#-using-the-api).

| Interface | Key |
|-----------|-----|
| `IPlayerCookiesAPIv2` | `Cookies.Player.v2` |
| `IServerCookiesAPIv2` | `Cookies.Server.v2` |
| `IPlayerCookiesAPIv1` | `Cookies.Player.v1` |
| `IServerCookiesAPIv1` | `Cookies.Server.v1` |

- `v2` is `v1` plus session values.
- Members can be called from any thread. A lookup for a player whose cookies haven't loaded yet waits for them, up to 5 seconds.
- Values are stored as JSON, public properties and fields included. `Get` returns a new copy each time: change it, then `Set` it again.
- A stored value that can't be read as `T` returns `default`. `Set` throws for a value that can't be serialised.
- Every plugin writes to the same values per player, so prefix your keys with your plugin's name.
- The `steamid` overloads work for players who aren't on the server. Bots share one entry, kept in memory only.
- `Load` and `Save` complete off the game thread: return to it with `Core.Scheduler.NextTick` before touching a player. Neither is needed in normal use.

### IPlayerCookiesAPIv1

| Member | Description |
|--------|-------------|
| `T? Get<T>(IPlayer player, string key)` / `(long steamid, string key)` | The value, or `default` when there is none |
| `T? GetOrDefault<T>(IPlayer player, string key, T defaultValue)` / `(long steamid, string key, T defaultValue)` | The value, or `defaultValue`. A missing value is stored once the player's cookies have loaded |
| `bool Has(IPlayer player, string key)` / `(long steamid, string key)` | Whether the key has a value |
| `void Set<T>(IPlayer player, string key, T value)` / `(long steamid, string key, T value)` | Store a value. Saved at once |
| `void Clear(IPlayer player)` / `(long steamid)` | Remove every value of the player, including other plugins' and other writers' |
| `void Unset(IPlayer player, string key)` / `(long steamid, string key)` | Remove a value |
| `Task Load(IPlayer player)` | Read the player's cookies again. Unsaved changes are kept |
| `Task Save(IPlayer player)` / `(long steamid)` | Save the player's unsaved changes now |

### IPlayerCookiesAPIv2

Session values: kept in memory, never saved, and cleared when the player leaves.

| Member | Description |
|--------|-------------|
| `T? GetSession<T>(IPlayer player, string key)` / `(long steamid, string key)` | The value, or `default`. A stored `T` is returned as the same object, not a copy |
| `T? GetSessionOrDefault<T>(IPlayer player, string key, T defaultValue)` / `(long steamid, string key, T defaultValue)` | The value, or `defaultValue`, which is stored |
| `bool HasSession(IPlayer player, string key)` / `(long steamid, string key)` | Whether the key has a value |
| `void SetSession<T>(IPlayer player, string key, T value)` / `(long steamid, string key, T value)` | Store a value |
| `void ClearSession(IPlayer player)` / `(long steamid)` | Remove every session value of the player |
| `void UnsetSession(IPlayer player, string key)` / `(long steamid, string key)` | Remove a value |

### IServerCookiesAPIv1

The one server-wide set, shared by every server on the database.

| Member | Description |
|--------|-------------|
| `T? Get<T>(string key)` | The value, or `default` when there is none |
| `T? GetOrDefault<T>(string key, T defaultValue)` | The value, or `defaultValue`. A missing value is stored once server cookies have loaded |
| `bool Has(string key)` | Whether the key has a value |
| `void Set<T>(string key, T value)` | Store a value. Saved at once |
| `void Clear()` | Remove every server value |
| `void Unset(string key)` | Remove a value |
| `Task Load()` | Read server cookies again. Unsaved changes are kept |
| `Task Save()` | Save unsaved changes now |

### IServerCookiesAPIv2

Server session values: kept in memory on this server until the plugin unloads.

| Member | Description |
|--------|-------------|
| `T? GetSession<T>(string key)` | The value, or `default`. A stored `T` is returned as the same object, not a copy |
| `T? GetSessionOrDefault<T>(string key, T defaultValue)` | The value, or `defaultValue`, which is stored |
| `bool HasSession(string key)` | Whether the key has a value |
| `void SetSession<T>(string key, T value)` | Store a value |
| `void ClearSession()` | Remove every server session value |
| `void UnsetSession(string key)` | Remove a value |

---

## 🧩 Using the API

### 1. Reference the contract

`Cookies.Contract.dll` is in the release, under `resources/exports/`.

```xml
<Reference Include="Cookies.Contract">
  <HintPath>path\to\Cookies.Contract.dll</HintPath>
  <Private>false</Private>
</Reference>
```

`Private` must be `false`. SwiftlyS2 loads the copy in Cookies' `resources/exports/`, and a second copy next to your plugin makes the interfaces stop matching, with no build error.

### 2. Get the interface

SwiftlyS2 runs `UseSharedInterface` again whenever a plugin loads or unloads. Assign the field on every run, so it is `null` while Cookies isn't loaded.

```csharp
private IPlayerCookiesAPIv2? _cookies;

public override void UseSharedInterface(IInterfaceManager interfaceManager) =>
    _cookies = interfaceManager.HasSharedInterface("Cookies.Player.v2")
        ? interfaceManager.GetSharedInterface<IPlayerCookiesAPIv2>("Cookies.Player.v2")
        : null;
```

### 3. Read and write

```csharp
var volume = _cookies?.GetOrDefault(player, "MyPlugin.Volume", 50) ?? 50;

_cookies?.Set(player, "MyPlugin.Volume", 80);
```

### When Cookies is optional

Without Cookies, `Cookies.Contract.dll` is missing, and a method that names a contract type throws as soon as it is compiled — in its caller, where its own `try` can't catch it. Name the contract types in one class only, mark its methods `[MethodImpl(MethodImplOptions.NoInlining)]`, and catch around the calls into it.

### Checklist

- [ ] `Cookies.Contract` referenced with `Private` set to `false`
- [ ] The interface assigned in `UseSharedInterface`, and a `null` one handled
- [ ] Keys prefixed with your plugin's name
- [ ] Contract types kept in one class, if Cookies is optional
- [ ] Back on the game thread after awaiting `Load` or `Save`, before touching a player

---

## 🔧 Building

### Requirements

| What | Version |
|------|---------|
| .NET SDK | 10 |
| SwiftlyS2 | Restored from NuGet |

### Build

```bash
git clone https://github.com/SwiftlyS2-Plugins/Cookies.git
cd Cookies
dotnet publish Cookies/Cookies.csproj -c Release
```

This writes the plugin to `Cookies/build/publish/Cookies/` and a zip of that folder to `Cookies/build/Cookies.zip`. Unzip it into `addons/swiftlys2/plugins/`.

### Layout

```
Cookies/                 the plugin
  src/api/               the four interfaces
  src/storage/           the cache: loading, saving, re-reading
  src/db/                SQL and migrations
Cookies.Contract/        the API other plugins reference, published to resources/exports/
```

---

## 🩺 Troubleshooting

Cookies writes to the server console, each line starting with `[Cookies]`. Most rows quote it.

| Problem | Cause | Solution |
|---------|-------|----------|
| `[Cookies] Failed to initialize database migrations.` | The `cookies` connection can't be reached, or its user lacks `CREATE` or `INDEX` rights | Fix the connection or the rights, then restart |
| `[Cookies] Loading cookies failed. Until the database works again, …`, or the same with `Saving cookies` or `Re-reading cookies` | The database can't be reached. With `No response within 5 seconds.`, it is too slow | Fix the database. `[Cookies] The database works again.` follows, and unsaved changes are saved |
| `[Cookies] Not saved for <SteamID64>: <keys>. The stored cookies would exceed the Data column's limit of <limit> <unit>.` | Those keys would take the player past the [size limit](#size-limit) | Report it to the plugin that writes those keys. The player's other changes were saved |
| `[Cookies] Stored cookies for <SteamID64> are not a JSON object; …` | `Data` was edited into something other than a JSON object | Fix the row. Otherwise the next save replaces it, and what it held is lost |
| `[Cookies] Saving cookies on unload timed out; changes not yet saved may be lost.` | The database took over 10 seconds while the plugin unloaded | Check the database. Changes not saved by then are lost |
| `[Cookies] Background maintenance failed; it runs again on the next cycle.` | An unexpected error | Report it, with the lines that follow |
| A change made on a website or another server shows up seconds later | Cached cookies are re-read every 5 seconds | Expected |
| A website's change was overwritten | The website wrote back a whole `Data` value | Change single keys — [Other writers](#other-writers) |
| Values set right after a player joins aren't saved | `SteamAuth.Mode` is `strict`, and Steam hasn't authorised them yet | Expected. Until then they share the bots' entry |
| Rows missing after upgrading | Duplicate rows for one SteamID. The first start keeps the lowest `Id` | Expected — [Upgrading](#upgrading) |
| A plugin can't find Cookies | Cookies isn't loaded, or the plugin ships its own `Cookies.Contract.dll` | Install Cookies. Reference the contract with `Private` `false` — [Using the API](#-using-the-api) |

---

## 🤝 Report issues or request features

- **Issues**: [GitHub Issues](https://github.com/SwiftlyS2-Plugins/Cookies/issues)
