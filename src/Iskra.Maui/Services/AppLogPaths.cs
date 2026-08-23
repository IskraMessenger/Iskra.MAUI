namespace Iskra.Maui.Services;

/// <summary>
/// Desktop: <c>{app}\logs</c>. Mobile: platform app-files logs folder.
/// </summary>
internal static class AppLogPaths
{
    public static string LogsDirectory { get; private set; } =
        Path.Combine(AppContext.BaseDirectory, "logs");

    public static void Initialize()
    {
        LogsDirectory = Resolve();
        Directory.CreateDirectory(LogsDirectory);
    }

    public static string TodayLogFile =>
        Path.Combine(LogsDirectory, DateTime.Now.ToString("dd.MM.yyyy") + ".log");

    public static IEnumerable<string> CandidateTodayFiles()
    {
        var today = DateTime.Now;
        var names = new[] { today.ToString("dd.MM.yyyy") + ".log", today.ToString("yyyy-MM-dd") + ".log" };
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            LogsDirectory,
            Path.Combine(AppContext.BaseDirectory, "logs")
        };
        try
        {
            dirs.Add(Path.Combine(FileSystem.AppDataDirectory, "logs"));
        }
        catch
        {
            // FileSystem not ready
        }

        foreach (var dir in dirs)
        foreach (var name in names)
            yield return Path.Combine(dir, name);
    }

    private static string Resolve()
    {
#if ANDROID
        try
        {
            var ext = Android.App.Application.Context.GetExternalFilesDir(null)?.AbsolutePath;
            if (!string.IsNullOrEmpty(ext))
                return Path.Combine(ext, "logs");
        }
        catch
        {
            // fall through
        }

        try
        {
            return Path.Combine(FileSystem.AppDataDirectory, "logs");
        }
        catch
        {
            // fall through
        }
#elif IOS || MACCATALYST
        try
        {
            return Path.Combine(FileSystem.AppDataDirectory, "Logs");
        }
        catch
        {
            // fall through
        }
#endif
        return Path.Combine(AppContext.BaseDirectory, "logs");
    }
}
