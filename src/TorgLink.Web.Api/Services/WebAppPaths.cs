namespace TorgLink.Web.Api.Services;

/// <summary>
/// Cross-platform app data: <c>TORGLINK_DATA_DIR</c>, else LocalApplicationData/TorgLink/Web
/// (~/.local/share on Linux, ~/Library/Application Support on macOS).
/// </summary>
internal static class WebAppPaths
{
    public static string AppRoot { get; private set; } = "";
    public static string LogsDirectory => Path.Combine(AppRoot, "logs");
    public static string DatabasePath => Path.Combine(AppRoot, "shortp2p.db");
    public static string RoutesDbPath => Path.Combine(AppRoot, "routes.db");
    public static string SessionDirectory => Path.Combine(AppRoot, "session");
    public static string PrefsPath => Path.Combine(AppRoot, "prefs.json");
    public static string ChatMediaPath => Path.Combine(AppRoot, "chat-media.json");

    public static void Initialize(string? contentRoot = null)
    {
        var fromEnv = Environment.GetEnvironmentVariable("TORGLINK_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            AppRoot = Path.GetFullPath(fromEnv);
        }
        else
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            AppRoot = string.IsNullOrWhiteSpace(local)
                ? Path.Combine(contentRoot ?? AppContext.BaseDirectory, "data")
                : Path.Combine(local, "TorgLink", "Web");
        }

        Directory.CreateDirectory(AppRoot);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(SessionDirectory);
    }
}
