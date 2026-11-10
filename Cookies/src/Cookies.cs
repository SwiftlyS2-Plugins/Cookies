using Cookies.API;
using Cookies.Contract;
using Cookies.Database;
using Cookies.Storage;
using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Plugins;

namespace Cookies;

[PluginMetadata(Id = "Cookies", Version = "1.0.15", Name = "Cookies", Author = "Swiftly Development Team")]
public partial class Cookies : BasePlugin
{
    private const string ConnectionName = "cookies";
    // Also the longest a change made elsewhere (another server, a website) takes to show up here.
    private const float MaintenanceIntervalSeconds = 5f;
    private static readonly TimeSpan UnloadFlushTimeout = TimeSpan.FromSeconds(10);

    private readonly CookieStore store;
    private readonly SessionStore sessions = new();
    private CancellationTokenSource? maintenanceTimer;

    public Cookies(ISwiftlyCore core) : base(core)
    {
        try
        {
            using var connection = core.Database.GetConnection(ConnectionName);

            MigrationRunner.RunMigrations(connection);
        }
        catch (Exception e)
        {
            core.Logger.LogError(e, "[Cookies] Failed to initialize database migrations.");
        }

        store = new CookieStore(new CookieRepository(() => core.Database.GetConnection(ConnectionName)), core.Logger);
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        interfaceManager.AddSharedInterface<IServerCookiesAPIv1, ServerCookiesAPIv1>(
            "Cookies.Server.v1", new ServerCookiesAPIv1(store)
        );
        interfaceManager.AddSharedInterface<IPlayerCookiesAPIv1, PlayerCookiesAPIv1>(
            "Cookies.Player.v1", new PlayerCookiesAPIv1(store)
        );
        interfaceManager.AddSharedInterface<IServerCookiesAPIv2, ServerCookiesAPIv2>(
            "Cookies.Server.v2", new ServerCookiesAPIv2(store, sessions)
        );
        interfaceManager.AddSharedInterface<IPlayerCookiesAPIv2, PlayerCookiesAPIv2>(
            "Cookies.Player.v2", new PlayerCookiesAPIv2(store, sessions)
        );
    }

    [EventListener<EventDelegates.OnClientPutInServer>]
    public void IOnClientPutInServerEvent(IOnClientPutInServerEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null || !player.IsValid) return;

        store.PlayerConnected(@event.PlayerId, (long)player.SteamID);
    }

    // In strict Steam auth mode a player reads as SteamID 0, the bots' entry, until Steam authorizes them.
    [EventListener<EventDelegates.OnClientSteamAuthorize>]
    public void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (player == null || !player.IsValid) return;

        store.PlayerConnected(@event.PlayerId, (long)player.SteamID);
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        if (store.PlayerDisconnected(@event.PlayerId) is not { } steamid) return;

        // A tick later, so other plugins' disconnect handlers can still read the session.
        Core.Scheduler.NextTick(() =>
        {
            if (!store.IsOnline(steamid)) sessions.Clear(steamid);
        });
    }

    public override void Load(bool hotReload)
    {
        maintenanceTimer?.Cancel();

        _ = store.LoadAsync(CookieStore.ServerId);

        // OnClientPutInServer never fires again for players already connected when the plugin (re)loads.
        foreach (var player in Core.PlayerManager.GetAllPlayers())
        {
            if (player.IsValid) store.PlayerConnected(player.PlayerID, (long)player.SteamID);
        }

        maintenanceTimer = Core.Scheduler.RepeatBySeconds(MaintenanceIntervalSeconds, () => _ = Task.Run(store.RunMaintenanceAsync));
    }

    public override void Unload()
    {
        maintenanceTimer?.Cancel();

        try
        {
            if (!Task.Run(() => store.FlushAsync(force: true)).Wait(UnloadFlushTimeout))
            {
                Core.Logger.LogWarning("[Cookies] Saving cookies on unload timed out; changes not yet saved may be lost.");
            }
        }
        catch (Exception e)
        {
            Core.Logger.LogError(e, "[Cookies] Failed to save cookies on unload.");
        }
    }
}
