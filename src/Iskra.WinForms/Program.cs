using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
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
        services.AddTransient<LoginForm>();
        services.AddTransient<RegisterForm>();
        services.AddTransient<MainForm>();
        services.AddTransient<AddChatForm>();
        services.AddTransient<MessengerServersForm>();

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

                provider.GetRequiredService<MessengerServerSyncService>().Start();
                using var main = provider.GetRequiredService<MainForm>();
                var result = main.ShowDialog();
                provider.GetRequiredService<MessengerServerSyncService>().StopAsync().GetAwaiter().GetResult();
                if (result != DialogResult.Retry)
                    return;
            }
        }
        finally
        {
            LogManager.Shutdown();
        }
    }
}
