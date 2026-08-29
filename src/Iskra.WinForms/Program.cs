using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.Discovery;
using ShortP2P.MessengerServer.Contracts.Dtos;
using ShortP2P.Transport;
using SQLitePCL;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Iskra.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Batteries_V2.Init();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var appRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Iskra", "WinForms");
        Directory.CreateDirectory(appRoot);

        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            b.ClearProviders();
            b.SetMinimumLevel(LogLevel.Information);
            b.AddNLog();
        });
        services.AddSingleton(_ => new AppDatabase(Path.Combine(appRoot, "iskra.db")));
        services.AddSingleton<IUserAuthRepository, SqliteUserAuthRepository>();
        services.AddSingleton<ISessionStorage>(_ => new FileSessionStorage(Path.Combine(appRoot, "session")));
        services.AddSingleton<AuthService>();
        services.AddSingleton<ChatRepository>();
        services.AddSingleton<ChatSessionCache>();
        services.AddSingleton<IMessengerServerRepository, SqliteMessengerServerRepository>();
        services.AddSingleton<DeviceIdProvider>();
        services.AddSingleton<MessengerServerManager>();
        services.AddSingleton<MessengerServerSyncService>();
        services.AddSingleton(_ => new P2pRoutingSettings
        {
            EnableUdpTransport = true,
            EnableBluetoothTransport = false
        });
        services.AddSingleton<IUdpTransportFactory>(sp =>
            new UdpTransportFactory(sp.GetService<ILoggerFactory>()));
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<P2pRoutingSettings>();
            var factory = sp.GetRequiredService<IUdpTransportFactory>();
            var sync = sp.GetRequiredService<MessengerServerSyncService>();
            var auth = sp.GetRequiredService<AuthService>();
            var chats = sp.GetRequiredService<ChatRepository>();
            var scan = new LocalNetworkScanner(settings, factory);
            // Same as UserP2pRuntime: GetClients first, then annotate/merge into discovery list.
            scan.PrioritizedExternalDiscoveryRound = async ct =>
            {
                try
                {
                    var remote = await sync.KeepAliveAndListRemoteClientsAsync(ct).ConfigureAwait(false);
                    scan.ApplyMessengerServerDirectory(ToDirectoryEntries(remote));
                    await SyncChatNicknamesFromPresenceAsync(auth, chats, remote, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Logged here: LocalNetworkScanner swallows external-round failures.
                    sp.GetService<ILoggerFactory>()
                        ?.CreateLogger("Iskra.WinForms.Discovery")
                        .LogWarning(ex, "PrioritizedExternalDiscoveryRound (GetClients) failed");
                    throw;
                }
            };
            return scan;
        });
        services.AddTransient<LoginForm>();
        services.AddTransient<RegisterForm>();
        services.AddTransient<MainForm>();
        services.AddTransient<AddChatForm>();
        services.AddTransient<MessengerServersForm>();
        // LanScanForm is constructed from MainForm with UI callbacks.

        if (File.Exists(Path.Combine(AppContext.BaseDirectory, "nlog.config")))
            LogManager.Setup().LoadConfigurationFromFile(Path.Combine(AppContext.BaseDirectory, "nlog.config"));

        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Iskra.WinForms");
        logger.LogInformation("Iskra WinForms (net48) started");

        try
        {
            while (true)
            {
                using var login = provider.GetRequiredService<LoginForm>();
                if (login.ShowDialog() != DialogResult.OK)
                    return;

                var auth = provider.GetRequiredService<AuthService>();
                var user = auth.CurrentUser;
                var scanner = provider.GetRequiredService<LocalNetworkScanner>();
                if (user != null)
                {
                    var peer = new PeerIdentity(
                        user.Nickname,
                        CompressedNetworkId.FromShortString(user.NetworkIdShort),
                        user.DataUdpPort);
                    scanner.StartAsync(peer).GetAwaiter().GetResult();
                }

                var sync = provider.GetRequiredService<MessengerServerSyncService>();
                var manager = provider.GetRequiredService<MessengerServerManager>();
                MessengerServersBootstrap.EnsureRunningAsync(sync, manager, logger).GetAwaiter().GetResult();
                try
                {
                    using var main = provider.GetRequiredService<MainForm>();
                    var result = main.ShowDialog();
                    sync.StopAsync().GetAwaiter().GetResult();
                    scanner.StopAsync().GetAwaiter().GetResult();
                    if (result != DialogResult.Retry)
                        return;
                }
                catch
                {
                    scanner.StopAsync().GetAwaiter().GetResult();
                    throw;
                }
            }
        }
        finally
        {
            LogManager.Shutdown();
        }
    }

    internal static MessengerServerDirectoryEntry[] ToDirectoryEntries(IReadOnlyList<ClientPresenceDto> remote)
    {
        var list = new MessengerServerDirectoryEntry[remote.Count];
        for (var i = 0; i < remote.Count; i++)
        {
            var c = remote[i];
            var lastSeen = DateTime.SpecifyKind(c.LastSeenAtUtc, DateTimeKind.Utc);
            list[i] = new MessengerServerDirectoryEntry(
                c.NetworkId.Trim(),
                c.Nick.Trim(),
                c.IsOnline,
                new DateTimeOffset(lastSeen));
        }

        return list;
    }

    private static async Task SyncChatNicknamesFromPresenceAsync(
        AuthService auth,
        ChatRepository chats,
        IReadOnlyList<ClientPresenceDto> remote,
        CancellationToken cancellationToken)
    {
        var user = auth.CurrentUser;
        if (user == null || remote.Count == 0)
            return;

        foreach (var client in remote)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var id = client.NetworkId.Trim();
            var nick = client.Nick?.Trim() ?? "";
            if (id.Length == 0 || nick.Length == 0)
                continue;
            if (string.Equals(nick, id, StringComparison.OrdinalIgnoreCase))
                continue;

            var chat = await chats.FindChatByPeerNetworkIdAsync(user.Id, id).ConfigureAwait(false);
            if (chat == null)
                continue;
            await chats.TryUpdatePeerNicknameAsync(chat.Id, nick).ConfigureAwait(false);
        }
    }
}
