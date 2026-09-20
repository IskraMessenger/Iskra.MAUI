using System.Text;

namespace TorgLink.Maui.Services;

/// <summary>Reads NLog file targets (Windows nlog.config and MAUI fallback layout).</summary>
public static class AppLogReader
{
    private const long MaxTailBytes = 768 * 1024;

    public static string? FindTodayLogPath()
    {
        foreach (var path in GetCandidateLogPaths())
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public static IReadOnlyList<string> GetCandidateLogPaths() =>
        AppLogPaths.CandidateTodayFiles().Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static string ReadTodayLog(out string? resolvedPath)
    {
        resolvedPath = FindTodayLogPath();
        if (resolvedPath == null)
            return "(No log file for today yet.)";

        return ReadTail(resolvedPath);
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
