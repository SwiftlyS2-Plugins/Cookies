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

The Cookies plugin provides two main APIs for managing persistent variables:

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