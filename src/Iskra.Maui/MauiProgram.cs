using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using NLog.Targets.Wrappers;
using ShortP2P.Auth;
using ShortP2P.Crypto;
using ShortP2P.Client.Bluetooth;
using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Data;
using ShortP2P.Client.Routing;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.Discovery;
using ShortP2P.Discovery.Ble;
using ShortP2P.Discovery.Pings;
using ShortP2P.Discovery.RouteTables;
using Iskra.Maui.Services;
using ShortP2P.Transport;
using ShortP2P.Transport.Abstractions;
#if ANDROID
using ShortP2P.Transport.Bluetooth.Android;
#endif

#if WINDOWS
using ShortP2P.Transport.Bluetooth.Windows;
#endif

namespace Iskra.Maui;

public static class MauiProgram
{
    private static bool _globalExceptionHandlersInstalled;
    public static IServiceProvider Services { get; private set; } = null!;

    public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
    {
#if ANDROID
        Android.Util.Log.Info("Iskra", "MauiProgram.CreateMauiApp");
#endif
        try
        {
#if ANDROID
            Android.Util.Log.Info("Iskra", "SQLite init");
#endif
            SQLitePCL.Batteries_V2.Init();
#if ANDROID
            Android.Util.Log.Info("Iskra", "SQLite init done");
#endif
        }
        catch (Exception ex)
        {
#if ANDROID
            Android.Util.Log.Error("Iskra", "SQLite init failed: " + ex);
#endif
            throw;
        }

        AppLogPaths.Initialize();
        ConfigureNLog();
        ConfigureGlobalExceptionHandlers();
        HttpServerResponseLog.Hook();
#if !ANDROID
        // Mono Android aborts in assembly.c if AssemblyLoad does extra work (TypeLoadException).
        HookAssemblyLoadLogging();
#endif

        var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
        builder.Logging.AddNLog();

        builder.Services.AddSingleton(_ =>
            ChatMediaOptions.LoadOrDefault(Path.Combine(FileSystem.AppDataDirectory, "chat-media.json")));
        builder.Services.AddSingleton(_ => new AppDatabase(Path.Combine(FileSystem.AppDataDirectory, "shortp2p.db")));
        builder.Services.AddSingleton<IUserAuthRepository, SqliteUserAuthRepository>();
        builder.Services.AddRouteDbContextWithPeerExpiryCleanup(
            Path.Combine(FileSystem.AppDataDirectory, "routes.db"), enableDiscovery: true);
        builder.Services.AddSingleton<ISessionStorage, MauiSecureStorage>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<ChatRepository>();
        builder.Services.AddSingleton<IBluetoothPresencePingTargetsProvider, BluetoothPresencePingTargetsProvider>();
        builder.Services.AddSingleton<IBleDiscoveredPeerStore, SqliteBleDiscoveredPeerStore>();
        builder.Services.AddSingleton<P2pRoutingSettingsStore>();
        builder.Services.AddSingleton<IUdpTransportFactory, UdpTransportFactory>();
        builder.Services.AddSingleton<ChatSessionCache>();
        builder.Services.AddSingleton<P2pCryptoSessionCache>();
        builder.Services.AddSingleton<IMessengerServerRepository, SqliteMessengerServerRepository>();
        builder.Services.AddSingleton<DeviceIdProvider>();
        builder.Services.AddSingleton<MessengerServerManager>();
        builder.Services.AddSingleton<MessengerServerSyncService>();
#if ANDROID
        builder.Services.AddSingleton<IBluetoothRadioCatalog, AndroidBluetoothRadioCatalog>();
        builder.Services.AddSingleton<IBleShortP2PPeripheralScanner>(_ =>
            new AndroidBluetoothLeShortP2PScanner(global::Android.App.Application.Context));
#elif WINDOWS
        builder.Services.AddSingleton<IBluetoothRadioCatalog, WindowsBluetoothRadioCatalog>();
        builder.Services.AddSingleton<IBleShortP2PPeripheralScanner>(sp =>
            new WindowsBluetoothLeShortP2PScanner(
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<WindowsBluetoothLeShortP2PScanner>()));
#endif
        builder.Services.AddSingleton(sp => new MauiBluetoothTransportRegistration(
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetService<IBleDiscoveredPeerStore>()));
        builder.Services.AddSingleton<IBluetoothTransportProvider>(sp =>
            sp.GetRequiredService<MauiBluetoothTransportRegistration>());
        builder.Services.AddSingleton(sp => new UserP2pRuntime(
            sp.GetRequiredService<P2pRoutingSettingsStore>(),
            sp.GetRequiredService<AuthService>(),
            sp.GetRequiredService<ChatRepository>(),
            sp.GetRequiredService<ChatMediaOptions>(),
            sp.GetRequiredService<IUdpTransportFactory>(),
            sp.GetRequiredService<ChatSessionCache>(),
            sp.GetRequiredService<P2pCryptoSessionCache>(),
            sp.GetService<IBluetoothTransportProvider>(),
            additionalDiscoveryTransports: null,
            sp.GetService<IRouteTableSnapshotSource>(),
            sp.GetService<IDiscoveryPingStore>(),
            sp.GetService<IBleShortP2PPeripheralScanner>(),
            sp.GetService<IBleDiscoveredPeerStore>(),
            sp.GetRequiredService<IBluetoothPresencePingTargetsProvider>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<MessengerServerSyncService>()));
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<ChatsPage>();
        builder.Services.AddTransient<ContactsPage>();
        builder.Services.AddTransient<NetworkPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<ChatDetailPage>();
        builder.Services.AddTransient<AddChatPage>();
        builder.Services.AddTransient<MyQrPage>();
        builder.Services.AddTransient<RoutingSettingsPage>();
        builder.Services.AddTransient<MessengerServersPage>();
        builder.Services.AddTransient<LanScanPage>();
        builder.Services.AddTransient<LogsPage>();
        builder.Services.AddTransient<AppShell>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        Services = app.Services;
        var logFactory = Services.GetRequiredService<ILoggerFactory>();
        AppLog.Initialize(logFactory);
        P2PSession.TrafficLogger = logFactory.CreateLogger<P2PSession>();
        IncomingMessageSound.EnsureHooked(Services.GetRequiredService<ChatRepository>(),
            logFactory.CreateLogger(nameof(IncomingMessageSound)));
        logFactory.CreateLogger<MauiHost>().LogInformation(
            "GUI application started. Logs directory: {LogsDir}", AppLogPaths.LogsDirectory);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => LogManager.Shutdown();
        return app;
    }

    private static void ConfigureNLog()
    {
        var logsDir = AppLogPaths.LogsDirectory;
        Directory.CreateDirectory(logsDir);

        var config = new LoggingConfiguration();
        var fileTarget = new FileTarget("gui-logfile")
        {
            FileName = Path.Combine(logsDir, "${date:format=dd.MM.yyyy}.log"),
            Layout =
                "${longdate}|${uppercase:${level}}|${logger}|${message}${onexception:inner=|${exception:format=tostring}}",
            Encoding = System.Text.Encoding.UTF8,
            KeepFileOpen = false,
            ConcurrentWrites = true,
            CreateDirs = true
        };

        var asyncTarget = new AsyncTargetWrapper(fileTarget, 1000, AsyncTargetWrapperOverflowAction.Discard);
        config.AddTarget(asyncTarget);
        config.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, asyncTarget,
            "ShortP2P.Transport.Bluetooth.*");
        config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, asyncTarget);
        LogManager.Configuration = config;
        LogManager.GetCurrentClassLogger().Info("NLog file target: {Path}", logsDir);
    }

    private static void HookAssemblyLoadLogging()
    {
        AppDomain.CurrentDomain.AssemblyLoad += (_, args) =>
        {
            var asm = args.LoadedAssembly;
            if (asm.IsDynamic)
                return;
            var location = string.IsNullOrEmpty(asm.Location) ? "(memory)" : asm.Location;
            AppLog.Files.LogDebug("Assembly loaded: {Name} location={Location}", asm.GetName().Name, location);
        };
    }

    private static void ConfigureGlobalExceptionHandlers()
    {
        if (_globalExceptionHandlersInstalled) return;

        _globalExceptionHandlersInstalled = true;
        var logger = LogManager.GetLogger("GlobalExceptions");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            logger.Error(ex, "UnhandledException. IsTerminating={IsTerminating}", args.IsTerminating);
            LogManager.Flush();
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            logger.Error(args.Exception, "UnobservedTaskException");
            args.SetObserved();
            LogManager.Flush();
        };
    }
}