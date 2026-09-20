using System.Text.Json;

namespace TorgLink.Web.Api.Services;

internal static class WebPrefs
{
    private static readonly object Gate = new();
    private static string _path = "";
    private static Dictionary<string, string> _map = new(StringComparer.Ordinal);

    public static void Initialize(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        if (!File.Exists(path))
            return;
        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (loaded != null)
                _map = new Dictionary<string, string>(loaded, StringComparer.Ordinal);
        }
        catch
        {
            _map = new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    public static string Get(string key, string defaultValue)
    {
        lock (Gate)
            return _map.TryGetValue(key, out var v) ? v : defaultValue;
    }

    public static bool Get(string key, bool defaultValue)
    {
        var raw = Get(key, defaultValue ? "true" : "false");
        return bool.TryParse(raw, out var b) ? b : defaultValue;
    }

    public static void Set(string key, string value)
    {
        lock (Gate)
        {
            _map[key] = value;
            if (string.IsNullOrEmpty(_path))
                return;
            File.WriteAllText(_path, JsonSerializer.Serialize(_map));
        }
    }

    public static void Set(string key, bool value) => Set(key, value ? "true" : "false");
}
