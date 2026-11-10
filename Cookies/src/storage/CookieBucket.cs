using System.Text.Json;

namespace Cookies.Storage;

internal enum BucketState
{
    NotLoaded,
    Loading,
    Loaded,
    Failed,
}

/// <summary>
/// One SteamID's cookies: the stored values with this server's unsaved changes applied on top.
/// Everything except <see cref="Io"/> and <see cref="IoEpoch"/> is guarded by <see cref="Sync"/>.
/// </summary>
internal sealed class CookieBucket
{
    private bool pendingClear;
    private Dictionary<string, JsonElement?> pending = new();
    private bool saveScheduled;
    private long lastSaveTicks;
    private int failedSaves;
    private long nextSaveTicks;

    public CookieBucket(long steamId, bool memoryOnly)
    {
        SteamId = steamId;
        MemoryOnly = memoryOnly;
        if (memoryOnly) State = BucketState.Loaded;
    }

    public long SteamId { get; }

    // Never loaded, saved or re-read: changes only ever live in Values.
    public bool MemoryOnly { get; }

    public object Sync { get; } = new();

    // Serializes this bucket's loads and saves, so stored snapshots are applied in commit order.
    public SemaphoreSlim Io { get; } = new(1, 1);

    // Bumped after every load and save; a batch refresh that read before a bump drops its older row.
    public int IoEpoch;

    public BucketState State { get; set; }

    public Task? LoadTask { get; set; }

    public bool Evicted { get; set; }

    public long LastAccessTicks { get; set; }

    public string? StoredJson { get; private set; }

    public Dictionary<string, JsonElement> Values { get; private set; } = new();

    public bool HasPendingChanges => pendingClear || pending.Count > 0;

    // A scheduled save is left to run on its own timing; force (unload) saves now regardless.
    public bool IsSaveDue(long now, bool force) => HasPendingChanges && (force || (!saveScheduled && now >= nextSaveTicks));

    // True when the caller should start a save after delayMs. Changes made while a save is scheduled or running wait
    // for the next one, and saves start at most once per minIntervalMs: a burst costs at most two round trips, and a
    // value rewritten every tick one save a second. During a failed save's backoff, the retry is left to maintenance.
    public bool TryScheduleSave(long now, long minIntervalMs, out long delayMs)
    {
        delayMs = 0;
        if (saveScheduled || !HasPendingChanges || now < nextSaveTicks) return false;

        saveScheduled = true;
        delayMs = Math.Max(0, lastSaveTicks + minIntervalMs - now);
        return true;
    }

    public void Set(string key, JsonElement value)
    {
        if (!MemoryOnly) pending[key] = value;
        Values[key] = value;
    }

    public void Unset(string key)
    {
        // Recorded even when absent here: another server may have stored the key since the last refresh.
        if (!MemoryOnly) pending[key] = null;
        Values.Remove(key);
    }

    public void Clear()
    {
        if (!MemoryOnly)
        {
            pendingClear = true;
            pending.Clear();
        }

        Values.Clear();
    }

    public void ApplyStored(string? json, Dictionary<string, JsonElement> stored)
    {
        Apply(stored, pendingClear, pending);
        Values = stored;
        StoredJson = json;
    }

    public bool TryTakePending(long now, out bool clear, out Dictionary<string, JsonElement?> changes)
    {
        saveScheduled = false;
        clear = pendingClear;
        changes = pending;
        if (!HasPendingChanges) return false;

        lastSaveTicks = now;
        pendingClear = false;
        pending = new Dictionary<string, JsonElement?>();
        return true;
    }

    public void CompleteSave(string json, Dictionary<string, JsonElement> stored)
    {
        failedSaves = 0;
        nextSaveTicks = 0;
        ApplyStored(json, stored);
    }

    public void FailSave(bool clear, Dictionary<string, JsonElement?> changes, long now)
    {
        // A Clear() made while the save was running already discards everything that failed.
        if (!pendingClear)
        {
            foreach (var (key, value) in pending)
            {
                changes[key] = value;
            }

            pending = changes;
            pendingClear = clear;
        }

        failedSaves++;
        nextSaveTicks = now + Math.Min(60_000L, 5_000L << Math.Min(failedSaves - 1, 4));
    }

    public static void Apply(Dictionary<string, JsonElement> target, bool clear, Dictionary<string, JsonElement?> changes)
    {
        if (clear) target.Clear();

        foreach (var (key, value) in changes)
        {
            if (value is { } element) target[key] = element;
            else target.Remove(key);
        }
    }
}
