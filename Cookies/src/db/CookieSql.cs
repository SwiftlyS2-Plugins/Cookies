using System.Data;

namespace Cookies.Database;

internal sealed class CookieSql
{
    // The Data column InitTable creates: TEXT on MySQL/MariaDB, character(16384) on PostgreSQL; SQLite has no limit.
    private static readonly CookieSql MySql = new(static name => $"`{name}`", rowLocks: true, arrayParameters: false, isSqlite: false,
        dataLimit: new DataLimit(65_535, InBytes: true));
    private static readonly CookieSql Postgres = new(static name => $"\"{name}\"", rowLocks: true, arrayParameters: true, isSqlite: false,
        dataLimit: new DataLimit(16_384, InBytes: false));
    private static readonly CookieSql Sqlite = new(static name => $"\"{name}\"", rowLocks: false, arrayParameters: false, isSqlite: true,
        dataLimit: null);

    private CookieSql(Func<string, string> quote, bool rowLocks, bool arrayParameters, bool isSqlite, DataLimit? dataLimit)
    {
        var table = quote("PlayerCookies");
        var id = quote("Id");
        var steamId = quote("SteamId64");
        var data = quote("Data");
        // PostgreSQL stores Data as character(16384), so untrimmed every row would arrive space-padded to 16 KB.
        var storedData = $"RTRIM({data})";

        IsSqlite = isSqlite;
        DataLimit = dataLimit;
        // Lowest Id first: SteamId64 is only unique once the UniqueSteamId64 migration has run.
        SelectOne = $"SELECT {id} AS Id, {storedData} AS Data FROM {table} WHERE {steamId} = @SteamId ORDER BY {id} LIMIT 1";
        SelectOneForUpdate = rowLocks ? $"{SelectOne} FOR UPDATE" : SelectOne;
        // Dapper hands an array to Npgsql as a single parameter, which only ANY() accepts.
        SelectMany = $"SELECT {steamId} AS SteamId64, {id} AS Id, {storedData} AS Data FROM {table} " +
                     $"WHERE {steamId} {(arrayParameters ? "= ANY(@SteamIds)" : "IN @SteamIds")} ORDER BY {steamId}, {id}";
        Insert = $"INSERT INTO {table} ({steamId}, {data}) VALUES (@SteamId, @Data)";
        Update = $"UPDATE {table} SET {data} = @Data WHERE {id} = @Id";
        // The aggregated derived table is what lets MySQL delete from a table its own subquery reads.
        DeleteDuplicates = $"DELETE FROM {table} WHERE {id} NOT IN " +
                           $"(SELECT KeepId FROM (SELECT MIN({id}) AS KeepId FROM {table} GROUP BY {steamId}) AS KeepRows)";
    }

    public bool IsSqlite { get; }
    public DataLimit? DataLimit { get; }
    public string SelectOne { get; }
    public string SelectOneForUpdate { get; }
    public string SelectMany { get; }
    public string Insert { get; }
    public string Update { get; }
    public string DeleteDuplicates { get; }

    public static CookieSql For(IDbConnection connection)
    {
        // Matched by name so both System.Data.SQLite and Microsoft.Data.Sqlite connections resolve.
        var name = connection.GetType().Name;
        if (name.Contains("MySql", StringComparison.OrdinalIgnoreCase)) return MySql;
        if (name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)) return Postgres;
        if (name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase)) return Sqlite;

        throw new NotSupportedException($"Unsupported database connection type: {connection.GetType().FullName}");
    }
}
