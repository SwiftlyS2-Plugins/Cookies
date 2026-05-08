<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>Cookies</strong></h2>
  <h3>Server/Client persistent variables.</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/build-passing-brightgreen" alt="Build Status">
  <img src="https://img.shields.io/github/downloads/SwiftlyS2-Plugins/Cookies/total" alt="Downloads">
  <img src="https://img.shields.io/github/stars/SwiftlyS2-Plugins/Cookies?style=flat&logo=github" alt="Stars">
  <img src="https://img.shields.io/github/license/SwiftlyS2-Plugins/Cookies" alt="License">
</p>

## Building

- Open the project in your preferred .NET IDE (e.g., Visual Studio, Rider, VS Code).
- Build the project. The output DLL and resources will be placed in the `build/` directory.
- The publish process will also create a zip file for easy distribution.

## Publishing

- Use the `dotnet publish -c Release` command to build and package your plugin.
- Distribute the generated zip file or the contents of the `build/publish` directory.

## Database Connection Key

The database connection is using the key `cookies`. It supports only SQLite, MySQL, MariaDB and PostgreSQL.

## API Reference

The Cookies plugin provides two main APIs for managing persistent variables.

### Interface Keys

To use these APIs in your plugin, retrieve them using the `IInterfaceManager` with the following keys:

| Interface | Key |
|-----------|-----|
| `IServerCookiesAPIv1` | `"Cookies.Server.v1"` |
| `IServerCookiesAPIv2` | `"Cookies.Server.v2"` |
| `IPlayerCookiesAPIv1` | `"Cookies.Player.v1"` |
| `IPlayerCookiesAPIv2` | `"Cookies.Player.v2"` |

**Example:**
```csharp
var serverCookiesV2 = interfaceManager.GetSharedInterface<IServerCookiesAPIv2>("Cookies.Server.v2");
var playerCookiesV2 = interfaceManager.GetSharedInterface<IPlayerCookiesAPIv2>("Cookies.Player.v2");
```

### Server Cookies API (`IServerCookiesAPIv1`)

Manage server-wide persistent variables that are shared across all players.

#### Methods

**`T? Get<T>(string key)`**
- Gets a variable from the server's data storage.
- **Parameters:**
  - `key` - The key of the variable.
- **Returns:** The value of the variable, or `null` if it doesn't exist.

**`bool Has(string key)`**
- Checks if a variable exists in the server's data storage.
- **Parameters:**
  - `key` - The key of the variable.
- **Returns:** `true` if the variable exists, `false` otherwise.

**`void Set<T>(string key, T value)`**
- Sets a variable in the server's data storage.
- **Parameters:**
  - `key` - The key of the variable.
  - `value` - The value of the variable.

**`void Unset(string key)`**
- Unsets a variable from the server's data storage.
- **Parameters:**
  - `key` - The key of the variable.

**`void Clear()`**
- Clears all stored data for the server.

**`void Load()`**
- Loads the server's cookies into memory.

**`void Save()`**
- Saves the server's cookies to the database.

### Server Cookies API v2 (`IServerCookiesAPIv2`)

Extended version of the Server Cookies API with additional convenience methods.

#### Methods (Inherited from v1)

- `T? Get<T>(string key)`
- `bool Has(string key)`
- `void Set<T>(string key, T value)`
- `void Unset(string key)`
- `void Clear()`
- `void Load()`
- `void Save()`

#### New Methods in v2

**`T? GetSession<T>(string key)`**
- Gets a variable from the server's data storage.
- **Parameters:**
  - `key` - The key of the variable.
- **Returns:** The value of the variable, or `null` if it doesn't exist.

**`T? GetSessionOrDefault<T>(string key, T defaultValue)`**
- Gets a variable from the server's data storage, or returns a default value if it doesn't exist.
- **Parameters:**
  - `key` - The key of the variable.
  - `defaultValue` - The default value to return if the variable doesn't exist.
- **Returns:** The value of the variable, or the default value if it doesn't exist.

**`bool HasSession(string key)`**
- Checks if a variable exists in the server's data storage.
- **Parameters:**
  - `key` - The key of the variable.
- **Returns:** `true` if the variable exists, `false` otherwise.

**`void SetSession<T>(string key, T value)`**
- Sets a variable in the server's data storage.
- **Parameters:**
  - `key` - The key of the variable.
  - `value` - The value of the variable.

**`void ClearSession()`**
- Clears all stored data for the server.

**`void UnsetSession(string key)`**
- Unsets a variable from the server's data storage.
- **Parameters:**
  - `key` - The key of the variable.

### Player Cookies API (`IPlayerCookiesAPIv1`)

Manage player-specific persistent variables. Each method has two overloads - one accepting an `IPlayer` object and another accepting a `steamid`.

#### Methods

**`T? Get<T>(IPlayer player, string key)`**
**`T? Get<T>(long steamid, string key)`**
- Gets a variable from the player's data storage.
- **Parameters:**
  - `player` / `steamid` - The player or their SteamID.
  - `key` - The key of the variable.
- **Returns:** The value of the variable, or `null` if it doesn't exist.

**`bool Has(IPlayer player, string key)`**
**`bool Has(long steamid, string key)`**
- Checks if a variable exists in the player's data storage.
- **Parameters:**
  - `player` / `steamid` - The player or their SteamID.
  - `key` - The key of the variable.
- **Returns:** `true` if the variable exists, `false` otherwise.

**`void Set<T>(IPlayer player, string key, T value)`**
**`void Set<T>(long steamid, string key, T value)`**
- Sets a variable in the player's data storage.
- **Parameters:**
  - `player` / `steamid` - The player or their SteamID.
  - `key` - The key of the variable.
  - `value` - The value of the variable.

### Player Cookies API v2 (`IPlayerCookiesAPIv2`)

Extended version of the Player Cookies API with additional convenience methods. All methods have two overloads - one accepting an `IPlayer` object and another accepting a `steamid`.

#### Methods (Inherited from v1)

- `T? Get<T>(IPlayer player, string key)`
- `T? Get<T>(long steamid, string key)`
- `bool Has(IPlayer player, string key)`
- `bool Has(long steamid, string key)`
- `void Set<T>(IPlayer player, string key, T value)`
- `void Set<T>(long steamid, string key, T value)`

#### New Methods in v2

**`T? GetSession<T>(IPlayer player, string key)`**
**`T? GetSession<T>(long steamid, string key)`**
- Gets a variable from the player's data storage.
- **Parameters:**
  - `player` / `steamid` - The player or their SteamID.
  - `key` - The key of the variable.
- **Returns:** The value of the variable, or `null` if it doesn't exist.

**`T? GetSessionOrDefault<T>(IPlayer player, string key, T defaultValue)`**
**`T? GetSessionOrDefault<T>(long steamid, string key, T defaultValue)`**
- Gets a variable from the player's data storage, or returns a default value if it doesn't exist.
- **Parameters:**
  - `player` / `steamid` - The player or their SteamID.
  - `key` - The key of the variable.
  - `defaultValue` - The default value to return if the variable doesn't exist.
- **Returns:** The value of the variable, or the default value if it doesn't exist.

**`bool HasSession(IPlayer player, string key)`**
**`bool HasSession(long steamid, string key)`**
- Checks if a variable exists in the player's data storage.
- **Parameters:**
  - `player` / `steamid` - The player or their SteamID.
  - `key` - The key of the variable.
- **Returns:** `true` if the variable exists, `false` otherwise.

**`void SetSession<T>(IPlayer player, string key, T value)`**
**`void SetSession<T>(long steamid, string key, T value)`**
- Sets a variable in the player's data storage.
- **Parameters:**
  - `player` / `steamid` - The player or their SteamID.
  - `key` - The key of the variable.
  - `value` - The value of the variable.

**`void Unset(IPlayer player, string key)`**
**`void Unset(long steamid, string key)`**
- Unsets a variable from the player's data storage.
- **Parameters:**
  - `player` / `steamid` - The player or their SteamID.
  - `key` - The key of the variable.

**`void Clear(IPlayer player)`**
**`void Clear(long steamid)`**
- Clears all stored data for the player.
- **Parameters:**
  - `player` / `steamid` - The player or their SteamID.

**`void Load(IPlayer player)`**
- Loads the player's cookies into memory.
- **Parameters:**
  - `player` - The player whose cookies should be loaded.

**`void Save(IPlayer player)`**
**`void Save(long steamid)`**
- Saves the player's cookies to the database.
- **Parameters:**
  - `player` / `steamid` - The player or their SteamID.