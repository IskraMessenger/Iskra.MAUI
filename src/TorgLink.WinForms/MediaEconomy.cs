using ShortP2P.Client.ChatMedia;
using ShortP2P.Discovery;

namespace TorgLink.WinForms;

/// <summary>
/// Media quality / traffic modes aligned with MAUI <c>MediaEconomy</c>
/// (TorgLink voice rates and economy image cap, not ShortP2P defaults).
/// </summary>
internal static class MediaEconomy
{
    /// <summary>Soft image cap in Economy / UltraEconomy (same as MAUI).</summary>
    public const int MaxImageBytes = 50 * 1024;

    public const int EconomyVoiceBitrateBps = 12_000;
    public const int UltraEconomyVoiceBitrateBps = 8_000;
    public const int MinVoiceBitrateBps = UltraEconomyVoiceBitrateBps;
    public const int DefaultSpeechBitrateBps = TrafficQualityModeExtensions.NormalVoiceBitrate;

    public static TrafficQualityMode Mode(P2pRoutingSettings settings) => settings.TrafficQuality;

    public static bool UsesReducedMedia(P2pRoutingSettings settings) =>
        Mode(settings) is TrafficQualityMode.Economy or TrafficQualityMode.UltraEconomy;

    public static int SpeechBitrate(P2pRoutingSettings settings) => SpeechBitrate(Mode(settings));

    public static int SpeechBitrate(TrafficQualityMode mode) =>
        mode switch
        {
            TrafficQualityMode.UltraEconomy => UltraEconomyVoiceBitrateBps,
            TrafficQualityMode.Economy => EconomyVoiceBitrateBps,
            _ => DefaultSpeechBitrateBps
        };

    public static int ImageLimit(ChatMediaOptions media, P2pRoutingSettings settings) =>
        UsesReducedMedia(settings) ? MaxImageBytes : media.MaxImageBytes;
}
