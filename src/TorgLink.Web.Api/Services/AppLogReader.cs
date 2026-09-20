using System.Text;

namespace TorgLink.Web.Api.Services;

internal static class AppLogReader
{
    private const long MaxTailBytes = 768 * 1024;

    public static string ReadTodayLog(out string? resolvedPath)
    {
        resolvedPath = FindTodayLogPath();
        return resolvedPath == null ? "(No log file for today yet.)" : ReadTail(resolvedPath);
    }

    public static string? FindTodayLogPath()
    {
        var today = DateTime.Now;
        var names = new[] { today.ToString("dd.MM.yyyy") + ".log", today.ToString("yyyy-MM-dd") + ".log" };
        foreach (var dir in new[] { WebAppPaths.LogsDirectory, Path.Combine(AppContext.BaseDirectory, "logs") })
        foreach (var name in names)
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string ReadTail(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var prefix = "";
            if (fs.Length > MaxTailBytes)
            {
                fs.Seek(fs.Length - MaxTailBytes, SeekOrigin.Begin);
                prefix = $"(Showing last {MaxTailBytes} bytes.)\n\n";
            }

            using var reader = new StreamReader(fs, Encoding.UTF8, true);
            return prefix + reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            return "Could not read log: " + ex.Message;
        }
    }
}
