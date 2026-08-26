using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Services;
using Iskra.Maui.Localization;

namespace Iskra.Maui.Services;

/// <summary>
/// Traffic-saving mode (WinForms-aligned): Opus 6 kbit/s, photos ≤50 KB, video 160×120.
/// Toggle: Settings → Ultra economy / Ультраэкономия (persisted in P2pRoutingSettings).
/// </summary>
internal static class MediaEconomy
{
    private const string PrefKey = "iskra.ultra_economy";

    /// <summary>WinForms <c>VoiceRecordHelper.TrafficSavingBitrate</c>.</summary>
    public const int SpeechBitrateBps = 6_000;

    /// <summary>WinForms <c>VoiceRecordHelper.DefaultBitrate</c>.</summary>
    public const int DefaultSpeechBitrateBps = 18_000;

    public const int MaxImageBytes = 50 * 1024;

    /// <summary>WinForms <c>VideoAttachHelper.TrafficSavingVideoWidth</c>.</summary>
    public const int VideoWidth = 160;

    /// <summary>WinForms <c>VideoAttachHelper.TrafficSavingVideoHeight</c>.</summary>
    public const int VideoHeight = 120;

    /// <summary>WinForms camera economy video bitrate.</summary>
    public const int VideoBitrateBps = 250_000;

    public const int NormalVideoWidth = 320;
    public const int NormalVideoHeight = 240;

    public static string Hint =>
        Loc.Tf("economy.hint", SpeechBitrateBps / 1000.0, MaxImageBytes / 1024, VideoWidth, VideoHeight);

    public static string VideoResolutionLabel => $"{VideoWidth}×{VideoHeight}";

    public static bool IsEnabled(UserP2pRuntime p2p) => p2p.Settings.TrafficSavingEnabled;

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
