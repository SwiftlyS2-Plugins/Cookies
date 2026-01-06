using System.Collections.Concurrent;
using Cookies.API;
using Cookies.Contract;
using Cookies.Database;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;

namespace Cookies;

[PluginMetadata(Id = "Cookies", Version = "1.0.2", Name = "Cookies", Author = "Swiftly Development Team")]
public partial class Cookies : BasePlugin
{
    private ConcurrentDictionary<long, Dictionary<string, object>> CachedCookies = new();
    private ConcurrentQueue<long> SaveQueue = new();
    private ConcurrentDictionary<long, IPlayer> playerBySteamId = new();
    private CancellationTokenSource? saveTaskCancellationTokenSource;
    private bool loaded = false;

    private IServerCookiesAPIv1? ServerCookiesAPIv1;
    private IPlayerCookiesAPIv1? PlayerCookiesAPIv1;

    public Cookies(ISwiftlyCore core) : base(core)
    {
        var connection = core.Database.GetConnection("cookies");
        var connectionString = core.Database.GetConnectionString("cookies");

        MigrationRunner.RunMigrations(connection, connectionString);
    }

    public override void ConfigureSharedInterface(IInterfaceManager interfaceManager)
    {
        interfaceManager.AddSharedInterface<IServerCookiesAPIv1, ServerCookiesAPIv1>(
            "Cookies.Server.v1", new ServerCookiesAPIv1(Core, ref CachedCookies, ref SaveQueue)
        );
        interfaceManager.AddSharedInterface<IPlayerCookiesAPIv1, PlayerCookiesAPIv1>(
            "Cookies.Player.v1", new PlayerCookiesAPIv1(Core, ref CachedCookies, ref SaveQueue, ref playerBySteamId)
        );
    }

    public override void UseSharedInterface(IInterfaceManager interfaceManager)
    {
        ServerCookiesAPIv1 = interfaceManager.GetSharedInterface<IServerCookiesAPIv1>("Cookies.Server.v1");
        PlayerCookiesAPIv1 = interfaceManager.GetSharedInterface<IPlayerCookiesAPIv1>("Cookies.Player.v1");

        if (!loaded)
        {
            loaded = true;
            Task.Run(() =>
            {
                ServerCookiesAPIv1.Load();
            });
        }
    }

    [EventListener<EventDelegates.OnClientSteamAuthorize>]
    public void OnClientSteamAuthorize(IOnClientSteamAuthorizeEvent @event)
    {
        var playerid = @event.PlayerId;

        Task.Run(() =>
        {
            if (PlayerCookiesAPIv1 == null) return;

            var player = Core.PlayerManager.GetPlayer(playerid);
            if (player == null) return;

            PlayerCookiesAPIv1.Load(player);
        });
    }

    [EventListener<EventDelegates.OnClientDisconnected>]
    public void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        var playerid = @event.PlayerId;

        var player = Core.PlayerManager.GetPlayer(playerid);
        if (player == null) return;

        var steamid = player.SteamID;

        Task.Run(() =>
        {
            if (PlayerCookiesAPIv1 == null) return;

            PlayerCookiesAPIv1.Save((long)steamid);

            CachedCookies.TryRemove((long)steamid, out _);
            playerBySteamId.TryRemove((long)steamid, out _);
        });
    }

    public override void Load(bool hotReload)
    {
        if (saveTaskCancellationTokenSource != null) saveTaskCancellationTokenSource.Cancel();

        saveTaskCancellationTokenSource = Core.Scheduler.RepeatBySeconds(10, () =>
        {
            Task.Run(() =>
            {
                while (SaveQueue.TryDequeue(out var steamid))
                {
                    if (steamid == -1) ServerCookiesAPIv1?.Save();
                    else
                    {
                        var player = Core.PlayerManager.GetAllPlayers().FirstOrDefault(p => p.IsValid && (long)p.SteamID == steamid);

                        if (player == null) continue;

                        PlayerCookiesAPIv1?.Save(player);
                    }
                }
            });
        });
    }

    public override void Unload()
    {
        saveTaskCancellationTokenSource?.Cancel();
    }
}