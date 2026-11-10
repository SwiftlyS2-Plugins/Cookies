using System.Data;
using System.Data.Common;
using Dapper;

namespace Cookies.Database;

internal sealed class CookieRepository
{
    private const int MaxSaveAttempts = 3;
    private const int ReadBatchSize = 500;

    // SQLite allows one writer, and in rollback-journal mode a commit locks readers out too, so this plugin's own
    // statements are queued instead of racing each other for the file lock ("database is locked").
    private static readonly SemaphoreSlim SqliteGate = new(1, 1);

    private readonly Func<IDbConnection> connectionFactory;

    public CookieRepository(Func<IDbConnection> connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public Task<string?> ReadAsync(long steamId) =>
        RunAsync(async (connection, sql) =>
        {
            var row = await connection.QueryFirstOrDefaultAsync<CookieRow>(sql.SelectOne, new { SteamId = steamId }).ConfigureAwait(false);
            return row?.Data;
        });

    /// <returns>The stored JSON for every requested SteamID, or null where it has no row.</returns>
    public Task<Dictionary<long, string?>> ReadManyAsync(IReadOnlyCollection<long> steamIds) =>
        RunAsync(async (connection, sql) =>
        {
            var result = new Dictionary<long, string?>(steamIds.Count);
            foreach (var chunk in steamIds.Chunk(ReadBatchSize))
            {
                var rows = await connection.QueryAsync<CookieRow>(sql.SelectMany, new { SteamIds = chunk }).ConfigureAwait(false);
                foreach (var row in rows)
                {
                    result.TryAdd(row.SteamId64, row.Data);
                }
            }

            foreach (var steamId in steamIds)
            {
                result.TryAdd(steamId, null);
            }

            return result;
        });

    /// <summary>
    /// Replaces the stored JSON with <paramref name="merge"/>(stored JSON, Data column limit) while holding the row, so
    /// writes another server made in the meantime are merged instead of overwritten.
    /// </summary>
    /// <returns>The JSON that is now stored.</returns>
    public async Task<string> SaveAsync(long steamId, Func<string?, DataLimit?, string> merge)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await RunAsync((connection, sql) => SaveOnceAsync(connection, sql, steamId, merge)).ConfigureAwait(false);
            }
            // A first insert lost to a concurrent one, or a deadlock: read the row again and redo the merge.
            catch (DbException) when (attempt < MaxSaveAttempts)
            {
            }
        }
    }

    private static async Task<string> SaveOnceAsync(DbConnection connection, CookieSql sql, long steamId, Func<string?, DataLimit?, string> merge)
    {
        // SQLite has no FOR UPDATE; Serializable makes System.Data.SQLite open the transaction with BEGIN IMMEDIATE.
        await using (var transaction = sql.IsSqlite
            ? await connection.BeginTransactionAsync(IsolationLevel.Serializable).ConfigureAwait(false)
            : await connection.BeginTransactionAsync().ConfigureAwait(false))
        {
            var row = await connection.QueryFirstOrDefaultAsync<CookieRow>(
                sql.SelectOneForUpdate, new { SteamId = steamId }, transaction).ConfigureAwait(false);

            if (row is not null)
            {
                var updated = merge(row.Data, sql.DataLimit);
                if (!string.Equals(row.Data, updated, StringComparison.Ordinal))
                {
                    await connection.ExecuteAsync(sql.Update, new { row.Id, Data = updated }, transaction).ConfigureAwait(false);
                }

                await transaction.CommitAsync().ConfigureAwait(false);
                return updated;
            }
        }

        // No row yet. Locking one that doesn't exist makes InnoDB lock the index gap instead, and two first saves
        // holding the same gap deadlock on their inserts, so the transaction is rolled back and the row inserted on
        // its own. A concurrent first save then fails on the unique index and is retried as an update.
        var created = merge(null, sql.DataLimit);
        await connection.ExecuteAsync(sql.Insert, new { SteamId = steamId, Data = created }).ConfigureAwait(false);
        return created;
    }

    private async Task<T> RunAsync<T>(Func<DbConnection, CookieSql, Task<T>> operation)
    {
        await using var connection = connectionFactory() as DbConnection
            ?? throw new NotSupportedException("The cookies database connection is not a DbConnection.");
        var sql = CookieSql.For(connection);
        var gate = sql.IsSqlite ? SqliteGate : null;

        if (gate is not null) await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await connection.OpenAsync().ConfigureAwait(false);
            return await operation(connection, sql).ConfigureAwait(false);
        }
        finally
        {
            gate?.Release();
        }
    }

    private sealed class CookieRow
    {
        public long SteamId64 { get; set; }
        public long Id { get; set; }
        public string? Data { get; set; }
    }
}
