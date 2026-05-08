using SwiftlyS2.Shared.Players;

namespace Cookies.Contract;

public interface IPlayerCookiesAPIv2 : IPlayerCookiesAPIv1
{
    /// <summary>
    /// Gets a variable from the player's data storage.
    /// </summary>
    /// <typeparam name="T">The type of the variable.</typeparam>
    /// <param name="key">The key of the variable.</param>
    /// <returns>The value of the variable, or null if it doesn't exist.</returns>
    public T? GetSession<T>(IPlayer player, string key);

    /// <summary>
    /// Gets a variable from the player's data storage by steamid.
    /// </summary>
    /// <typeparam name="T">The type of the variable.</typeparam>
    /// <param name="steamid">The steamid of the player.</param>
    /// <param name="key">The key of the variable.</param>
    /// <returns>The value of the variable, or null if it doesn't exist.</returns>
    public T? GetSession<T>(long steamid, string key);

    /// <summary>
    /// Gets a variable from the player's data storage, or returns a default value if it doesn't exist.
    /// </summary>
    /// <typeparam name="T">The type of the variable.</typeparam>
    /// <param name="player">The player object.</param>
    /// <param name="key">The key of the variable.</param>
    /// <param name="defaultValue">The default value to return if the variable doesn't exist.</param>
    /// <returns>The value of the variable, or the default value if it doesn't exist.</returns>
    public T? GetSessionOrDefault<T>(IPlayer player, string key, T defaultValue);

    /// <summary>
    /// Gets a variable from the player's data storage by steamid, or returns a default value if it doesn't exist.
    /// </summary>
    /// <typeparam name="T">The type of the variable.</typeparam>
    /// <param name="steamid">The steamid of the player.</param>
    /// <param name="key">The key of the variable.</param>
    /// <param name="defaultValue">The default value to return if the variable doesn't exist.</param>
    /// <returns>The value of the variable, or the default value if it doesn't exist.</returns>
    public T? GetSessionOrDefault<T>(long steamid, string key, T defaultValue);

    /// <summary>
    /// Checks if a variable exists in the player's data storage.
    /// </summary>
    /// <param name="key">The key of the variable.</param>
    /// <returns>True if the variable exists, false otherwise.</returns>
    public bool HasSession(IPlayer player, string key);

    /// <summary>
    /// Checks if a variable exists in the player's data storage by steamid.
    /// </summary>
    /// <param name="steamid">The steamid of the player.</param>
    /// <param name="key">The key of the variable.</param>
    /// <returns>True if the variable exists, false otherwise.</returns>
    public bool HasSession(long steamid, string key);

    /// <summary>
    /// Sets a variable in the player's data storage.
    /// </summary>
    /// <typeparam name="T">The type of the variable.</typeparam>
    /// <param name="key">The key of the variable.</param>
    /// <param name="value">The value of the variable.</param>
    public void SetSession<T>(IPlayer player, string key, T value);

    /// <summary>
    /// Sets a variable in the player's data storage by steamid.
    /// </summary>
    /// <typeparam name="T">The type of the variable.</typeparam>
    /// <param name="steamid">The steamid of the player.</param>
    /// <param name="key">The key of the variable.</param>
    /// <param name="value">The value of the variable.</param>
    public void SetSession<T>(long steamid, string key, T value);

    /// <summary>
    /// Clears all stored data for the player.
    /// </summary>
    public void ClearSession(IPlayer player);

    /// <summary>
    /// Clears all stored data for the player by steamid.
    /// </summary>
    /// <param name="steamid">The steamid of the player.</param>
    public void ClearSession(long steamid);

    /// <summary>
    /// Unsets a variable from the player's data storage.
    /// </summary>
    /// <param name="key">The key of the variable.</param>
    public void UnsetSession(IPlayer player, string key);

    /// <summary>
    /// Unsets a variable from the player's data storage by steamid.
    /// </summary>
    /// <param name="steamid">The steamid of the player.</param>
    /// <param name="key">The key of the variable.</param>
    public void UnsetSession(long steamid, string key);
}