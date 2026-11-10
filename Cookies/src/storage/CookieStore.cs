using System.Collections.Concurrent;
using System.Text.Json;
using Cookies.Database;
using Microsoft.Extensions.Logging;

namespace Cookies.Storage;

/// <summary>
/// Cookies for every SteamID used on this server. Reads come from memory; each change is saved per key as soon as
/// it is made, and <see cref="RunMaintenanceAsync"/> re-checks cached rows so other writers' changes show up.
/// </summary>
internal sealed class CookieStore
{
    public const long ServerId = -1;

    // Every bot reports SteamID 0, so a bot's cookies would be one value shared by all bots; they stay in memory.
    public const long BotId = 0;

    // Bounds how many connections a burst of changes (e.g. every player's stats at round end) can hold at once.
    private const int MaxConcurrentSaves = 8;
    private const long MinSaveIntervalMs = 1_000;
    private const long IdleEvictionMs = 120_000;
    private static readonly TimeSpan BlockingLoadTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<long, CookieBucket> buckets = new();
    private readonly ConcurrentDictionary<int, long> onlinePlayers = new();
    private readonly SemaphoreSlim maintenanceGate = new(1, 1);
    private readonly SemaphoreSlim saveSlots = new(MaxConcurrentSaves, MaxConcurrentSaves);
    private readonly CookieRepository repository;
    private readonly ILogger logger;
    private int databaseFailing;

    public CookieStore(CookieRepository repository, ILogger logger)
    {
        this.repository = repository;
        this.logger = logger;
    }

    public bool TryGet(long steamId, string key, out JsonElement value)
    {
        var bucket = AcquireLoaded(steamId);
        lock (bucket.Sync)
        {
            return bucket.Values.TryGetValue(key, out value);
        }
    }

    // Until the row is loaded the stored value is unknown, and saving a default would overwrite it.
    public void SetIfAbsent(long steamId, string key, JsonElement value) =>
        Mutate(steamId, bucket =>
        {
            if (bucket.State == BucketState.Loaded && !bucket.Values.ContainsKey(key)) bucket.Set(key, value);
        });

    public void Set(long steamId, string key, JsonElement value) => Mutate(steamId, bucket => bucket.Set(key, value));

    public void Unset(long steamId, string key) => Mutate(steamId, bucket => bucket.Unset(key));

    public void Clear(long steamId) => Mutate(steamId, static bucket => bucket.Clear());

    /// <summary>Reads the row again; unsaved changes stay applied on top of it.</summary>
    public Task LoadAsync(long steamId)
    {
        while (true)
        {
            var bucket = GetOrAddBucket(steamId);
            lock (bucket.Sync)
            {
                if (bucket.Evicted) continue;

                bucket.LastAccessTicks = Environment.TickCount64;
                if (bucket.MemoryOnly) return Task.CompletedTask;

                return bucket.State switch
                {
                    BucketState.Loading => bucket.LoadTask!,
                    BucketState.Loaded => Task.Run(() => LoadBucketAsync(bucket)),
                    _ => StartLoad(bucket),
                };
            }
        }
    }

    public Task SaveAsync(long steamId) =>
        buckets.TryGetValue(steamId, out var bucket) ? Task.Run(() => SaveBucketAsync(bucket)) : Task.CompletedTask;

    /// <param name="force">Also save changes still waiting out a failed save's backoff, and wait for saves already running.</param>
    public Task FlushAsync(bool force = false)
    {
        var now = Environment.TickCount64;
        var due = new List<CookieBucket>();
        foreach (var bucket in buckets.Values)
        {
            lock (bucket.Sync)
            {
                if (bucket.IsSaveDue(now, force) || (force && bucket.Io.CurrentCount == 0)) due.Add(bucket);
            }
        }

        return due.Count == 0 ? Task.CompletedTask : Task.WhenAll(due.Select(SaveBucketAsync));
    }

    public void PlayerConnected(int playerId, long steamId)
    {
        // Steam authorization re-reports the same SteamID unless strict auth mode hid it until then; no reload needed.
        if (onlinePlayers.TryGetValue(playerId, out var current) && current == steamId) return;

        onlinePlayers[playerId] = steamId;
        _ = LoadAsync(steamId);
    }

    /// <returns>The SteamID that was on <paramref name="playerId"/>, or null if it was not tracked.</returns>
    public long? PlayerDisconnected(int playerId)
    {
        if (!onlinePlayers.TryRemove(playerId, out var steamId)) return null;

        _ = SaveAsync(steamId);
        return steamId;
    }

    public bool IsOnline(long steamId) => onlinePlayers.Values.Contains(steamId);

    public async Task RunMaintenanceAsync()
    {
        if (!maintenanceGate.Wait(0)) return;

        try
        {
            await FlushAsync().ConfigureAwait(false);

            try
            {
                if (await RefreshAsync().ConfigureAwait(false)) ReportSuccess();
            }
            catch (Exception e)
            {
                ReportFailure("Re-reading cookies", e);
            }

            EvictIdle(Environment.TickCount64);
        }
        catch (Exception e)
        {
            logger.LogError(e, "[Cookies] Background maintenance failed; it runs again on the next cycle.");
        }
        finally
        {
            maintenanceGate.Release();
        }
    }

    private CookieBucket GetOrAddBucket(long steamId) =>
        buckets.GetOrAdd(steamId, static id => new CookieBucket(id, memoryOnly: id == BotId));

    private CookieBucket AcquireLoaded(long steamId)
    {
        while (true)
        {
            var bucket = GetOrAddBucket(steamId);
            Task? load;
            lock (bucket.Sync)
            {
                if (bucket.Evicted) continue;

                bucket.LastAccessTicks = Environment.TickCount64;
                load = bucket.State switch
                {
                    BucketState.NotLoaded => StartLoad(bucket),
                    BucketState.Loading => bucket.LoadTask,
                    _ => null,
                };
            }

            if (load is not null) WaitForLoad(load);
            return bucket;
        }
    }

    // The API is synchronous, so a first lookup must wait for its row. While the database is failing, including after
    // a wait times out, lookups don't wait at all: a hung database costs one hitch rather than one per player.
    private void WaitForLoad(Task load)
    {
        if (load.IsCompleted || Volatile.Read(ref databaseFailing) == 1) return;
        if (load.Wait(BlockingLoadTimeout)) return;

        ReportFailure("Loading cookies", new TimeoutException($"No response within {BlockingLoadTimeout.TotalSeconds} seconds."));
    }

    private void Mutate(long steamId, Action<CookieBucket> change)
    {
        while (true)
        {
            var bucket = GetOrAddBucket(steamId);
            bool save;
            long delayMs;
            lock (bucket.Sync)
            {
                if (bucket.Evicted) continue;

                bucket.LastAccessTicks = Environment.TickCount64;
                change(bucket);
                save = bucket.TryScheduleSave(Environment.TickCount64, MinSaveIntervalMs, out delayMs);
            }

            if (save) StartSave(bucket, delayMs);
            return;
        }
    }

    private void StartSave(CookieBucket bucket, long delayMs) =>
        _ = Task.Run(async () =>
        {
            if (delayMs > 0) await Task.Delay(TimeSpan.FromMilliseconds(delayMs)).ConfigureAwait(false);
            await SaveBucketAsync(bucket).ConfigureAwait(false);
        });

    private Task StartLoad(CookieBucket bucket)
    {
        bucket.State = BucketState.Loading;
        return bucket.LoadTask = Task.Run(() => LoadBucketAsync(bucket));
    }

    private async Task LoadBucketAsync(CookieBucket bucket)
    {
        try
        {
            await bucket.Io.WaitAsync().ConfigureAwait(false);
            try
            {
                var json = await repository.ReadAsync(bucket.SteamId).ConfigureAwait(false);
                var stored = Parse(bucket.SteamId, json);
                lock (bucket.Sync)
                {
                    bucket.ApplyStored(json, stored);
                    bucket.State = BucketState.Loaded;
                }

                Interlocked.Increment(ref bucket.IoEpoch);
                ReportSuccess();
            }
            finally
            {
                bucket.Io.Release();
            }
        }
        catch (Exception e)
        {
            lock (bucket.Sync)
            {
                // A failed reload keeps serving the values already cached.
                if (bucket.State == BucketState.Loading) bucket.State = BucketState.Failed;
            }

            ReportFailure("Loading cookies", e);
        }
    }

    private async Task SaveBucketAsync(CookieBucket bucket)
    {
        await bucket.Io.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (bucket.Sync)
            {
                if (!bucket.HasPendingChanges) return;
            }

            // Changes are taken once a slot is free, so ones made while waiting for it go out in this save too.
            await saveSlots.WaitAsync().ConfigureAwait(false);
            try
            {
                await SavePendingAsync(bucket).ConfigureAwait(false);
            }
            finally
            {
                saveSlots.Release();
            }
        }
        finally
        {
            bucket.Io.Release();
        }
    }

    // The caller holds bucket.Io and a save slot.
    private async Task SavePendingAsync(CookieBucket bucket)
    {
        bool clear;
        Dictionary<string, JsonElement?> changes;
        lock (bucket.Sync)
        {
            if (!bucket.TryTakePending(Environment.TickCount64, out clear, out changes)) return;
        }

        try
        {
            Dictionary<string, JsonElement>? merged = null;
            List<string>? dropped = null;
            DataLimit? limit = null;
            var json = await repository.SaveAsync(bucket.SteamId, (stored, columnLimit) =>
            {
                limit = columnLimit;
                var previous = clear ? new Dictionary<string, JsonElement>() : Parse(bucket.SteamId, stored);
                merged = new Dictionary<string, JsonElement>(previous);
                CookieBucket.Apply(merged, clear, changes);
                var serialized = CookieJson.Serialize(merged);

                dropped = null;
                if (columnLimit is { } max && !max.Fits(serialized))
                {
                    dropped = [];
                    serialized = FitToLimit(previous, merged, changes, max, dropped, stored);
                }

                return serialized;
            }).ConfigureAwait(false);

            lock (bucket.Sync)
            {
                bucket.CompleteSave(json, merged!);
                bucket.State = BucketState.Loaded;
            }

            Interlocked.Increment(ref bucket.IoEpoch);
            ReportSuccess();

            if (dropped is { Count: > 0 } && limit is { } exceeded)
            {
                logger.LogError(
                    "[Cookies] Not saved for {SteamId}: {Keys}. The stored cookies would exceed the Data column's limit of {Limit} {Unit}.",
                    bucket.SteamId, string.Join(", ", dropped), exceeded.Max, exceeded.Unit);
            }
        }
        catch (Exception e)
        {
            lock (bucket.Sync)
            {
                bucket.FailSave(clear, changes, Environment.TickCount64);
            }

            ReportFailure("Saving cookies", e);
        }
    }

    // Reverts this save's largest new values, one at a time, until the row fits the column again.
    private static string FitToLimit(Dictionary<string, JsonElement> previous, Dictionary<string, JsonElement> merged,
        Dictionary<string, JsonElement?> changes, DataLimit limit, List<string> dropped, string? stored)
    {
        var largestFirst = changes
            .Where(static change => change.Value is not null)
            .OrderByDescending(static change => change.Value!.Value.GetRawText().Length)
            .Select(static change => change.Key);

        foreach (var key in largestFirst)
        {
            if (previous.TryGetValue(key, out var old)) merged[key] = old;
            else merged.Remove(key);

            dropped.Add(key);
            var json = CookieJson.Serialize(merged);
            if (limit.Fits(json)) return json;
        }

        // Only reachable if the stored row itself measures over the limit: leave it as it is rather than fail forever.
        merged.Clear();
        foreach (var (key, value) in previous)
        {
            merged[key] = value;
        }

        dropped.Clear();
        dropped.AddRange(changes.Keys);
        return stored ?? CookieJson.Serialize(merged);
    }

    /// <summary>One query for every cached row; a row is only re-parsed when its stored JSON differs from the last one seen.</summary>
    /// <returns>Whether the database was queried.</returns>
    private async Task<bool> RefreshAsync()
    {
        var targets = new List<(CookieBucket Bucket, int Epoch)>();
        foreach (var bucket in buckets.Values)
        {
            lock (bucket.Sync)
            {
                if (bucket.MemoryOnly || bucket.State is not (BucketState.Loaded or BucketState.Failed)) continue;
            }

            targets.Add((bucket, Volatile.Read(ref bucket.IoEpoch)));
        }

        if (targets.Count == 0) return false;

        var rows = await repository.ReadManyAsync(targets.ConvertAll(static target => target.Bucket.SteamId)).ConfigureAwait(false);

        foreach (var (bucket, epoch) in targets)
        {
            // A load or save that is running, or ran after the read, has applied a row at least this new.
            if (!bucket.Io.Wait(0)) continue;

            try
            {
                if (Volatile.Read(ref bucket.IoEpoch) != epoch) continue;

                var json = rows[bucket.SteamId];
                lock (bucket.Sync)
                {
                    if (bucket.State == BucketState.Loaded && string.Equals(bucket.StoredJson, json, StringComparison.Ordinal)) continue;
                }

                var stored = Parse(bucket.SteamId, json);
                lock (bucket.Sync)
                {
                    bucket.ApplyStored(json, stored);
                    bucket.State = BucketState.Loaded;
                }
            }
            finally
            {
                bucket.Io.Release();
            }
        }

        return true;
    }

    private void EvictIdle(long now)
    {
        var online = onlinePlayers.Values.ToHashSet();
        foreach (var bucket in buckets.Values)
        {
            if (bucket.SteamId == ServerId || online.Contains(bucket.SteamId)) continue;

            lock (bucket.Sync)
            {
                if (now - bucket.LastAccessTicks < IdleEvictionMs || bucket.HasPendingChanges
                    || bucket.State == BucketState.Loading || bucket.Io.CurrentCount == 0)
                {
                    continue;
                }

                // Marked under the lock so a concurrent writer retries on a fresh bucket instead of this detached one.
                bucket.Evicted = true;
                buckets.TryRemove(new KeyValuePair<long, CookieBucket>(bucket.SteamId, bucket));
            }
        }
    }

    // One error when the database starts failing and one line once it works again, however many operations fail between.
    private void ReportFailure(string operation, Exception exception)
    {
        if (Interlocked.Exchange(ref databaseFailing, 1) == 0)
        {
            logger.LogError(exception,
                "[Cookies] {Operation} failed. Until the database works again, lookups don't wait for it, unsaved changes are retried and further failures aren't logged.",
                operation);
        }
    }

    private void ReportSuccess()
    {
        if (Volatile.Read(ref databaseFailing) == 1 && Interlocked.Exchange(ref databaseFailing, 0) == 1)
        {
            logger.LogInformation("[Cookies] The database works again.");
        }
    }

    private Dictionary<string, JsonElement> Parse(long steamId, string? json)
    {
        try
        {
            return CookieJson.Parse(json);
        }
        catch (JsonException e)
        {
            logger.LogWarning(e, "[Cookies] Stored cookies for {SteamId} are not a JSON object; treating them as empty, so the next save replaces them.", steamId);
            return new Dictionary<string, JsonElement>();
        }
    }
}
