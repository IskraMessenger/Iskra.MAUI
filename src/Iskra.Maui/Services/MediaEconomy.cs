using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;
using Iskra.Maui.Localization;

namespace Iskra.Maui.Services;

/// <summary>
/// Media quality / traffic modes aligned with ShortP2P <see cref="TrafficQualityMode"/>.
/// Selection: Settings → traffic quality (persisted in P2pRoutingSettings.TrafficQuality).
/// </summary>
internal static class MediaEconomy
{
    /// <summary>Soft image cap in Economy / UltraEconomy (Iskra-specific; WinForms does not compress photos).</summary>
    public const int MaxImageBytes = 50 * 1024;

    public const int MinVoiceBitrateBps = TrafficQualityModeExtensions.UltraEconomyVoiceBitrate;

    public const int DefaultSpeechBitrateBps = TrafficQualityModeExtensions.NormalVoiceBitrate;

    public static TrafficQualityMode Mode(UserP2pRuntime p2p) => p2p.Settings.TrafficQuality;

    public static bool UsesReducedMedia(UserP2pRuntime p2p) =>
        Mode(p2p) is TrafficQualityMode.Economy or TrafficQualityMode.UltraEconomy;

    public static void Apply(UserP2pRuntime p2p, TrafficQualityMode mode)
    {
        p2p.Settings.TrafficQuality = mode;
    }

    public static int SpeechBitrate(UserP2pRuntime p2p) => Mode(p2p).GetVoiceBitrate();

    public static (int Width, int Height) VideoResolution(UserP2pRuntime p2p) =>
        Mode(p2p).GetVideoResolution();

    public static string VideoResolutionLabel(UserP2pRuntime p2p)
    {
        var (w, h) = VideoResolution(p2p);
        return $"{w}×{h}";
    }

    public static int CameraVideoBitrate(UserP2pRuntime p2p) => Mode(p2p).GetCameraVideoBitrate();

    public static int ImageLimit(ChatMediaOptions media, UserP2pRuntime p2p) =>
        UsesReducedMedia(p2p) ? MaxImageBytes : media.MaxImageBytes;

    public static string Hint(TrafficQualityMode mode)
    {
        var (w, h) = mode.GetVideoResolution();
        var photoKb = mode is TrafficQualityMode.Normal ? null : (int?)(MaxImageBytes / 1024);
        return photoKb is null
            ? Loc.Tf("economy.hint_normal", mode.GetVoiceBitrate() / 1000.0, w, h)
            : Loc.Tf("economy.hint", mode.GetVoiceBitrate() / 1000.0, photoKb.Value, w, h);
    }

    public static string ModeLabel(TrafficQualityMode mode) =>
        mode switch
        {
            TrafficQualityMode.UltraEconomy => Loc.T("economy.mode.ultra"),
            TrafficQualityMode.Economy => Loc.T("economy.mode.economy"),
            _ => Loc.T("economy.mode.normal")
        };

    public static IReadOnlyList<TrafficQualityMode> AllModes { get; } =
    [
        TrafficQualityMode.Normal,
        TrafficQualityMode.Economy,
        TrafficQualityMode.UltraEconomy
    ];
}
