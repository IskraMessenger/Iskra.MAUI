using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Services;

namespace Iskra.Maui.Services;

/// <summary>
/// Ультраэкономия трафика: Opus 4.8 кбит/с, фото до 50 КБ, видео 144p (256×144).
/// </summary>
internal static class MediaEconomy
{
    private const string PrefKey = "iskra.ultra_economy";

    public const int SpeechBitrateBps = 4_800;
    public const int DefaultSpeechBitrateBps = 18_000;
    public const int MaxImageBytes = 50 * 1024;
    public const int VideoWidth = 256;
    public const int VideoHeight = 144;
    public const int VideoBitrateBps = 150_000;

    public static string Hint =>
        $"Речь {SpeechBitrateBps / 1000.0:0.0} кбит/с, фото до {MaxImageBytes / 1024} КБ, видео {VideoHeight}p ({VideoWidth}×{VideoHeight})";

    public static bool IsEnabled(UserP2pRuntime p2p) =>
        Preferences.Default.ContainsKey(PrefKey)
            ? Preferences.Default.Get(PrefKey, false)
            : p2p.Settings.TrafficSavingEnabled;

    public static void Apply(UserP2pRuntime p2p, bool enabled)
    {
        Preferences.Default.Set(PrefKey, enabled);
        p2p.Settings.TrafficSavingEnabled = enabled;
    }

    public static int SpeechBitrate(UserP2pRuntime p2p) =>
        IsEnabled(p2p) ? SpeechBitrateBps : DefaultSpeechBitrateBps;

    public static int ImageLimit(ChatMediaOptions media, UserP2pRuntime p2p) =>
        IsEnabled(p2p) ? MaxImageBytes : media.MaxImageBytes;
}
