using ShortP2P.Discovery;

namespace Iskra.Maui.Services;

/// <summary>
/// Prepares outgoing video for the selected <see cref="TrafficQualityMode"/>.
/// Hardware H.264 pass to 240p; optional software pass to 144p when the settings checkbox is on
/// (UltraEconomy). Windows and Android re-encode; other platforms pass through.
/// </summary>
internal static class Video144pTranscoder
{
    private const int TargetFps = 15;

    public static bool IsVideoMime(string mime) =>
        mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

    public static async Task<(bool Ok, byte[]? Bytes, string Mime, string FileName, string? Error)> PrepareAsync(
        string sourcePath, string sourceFileName, string sourceMime, TrafficQualityMode mode)
    {
        if (mode == TrafficQualityMode.Normal)
        {
            var raw = await File.ReadAllBytesAsync(sourcePath).ConfigureAwait(false);
            return (true, raw, sourceMime, sourceFileName, null);
        }

        var software144 = MediaEconomy.WantsSoftware144p(mode);

#if ANDROID
        return await TranscodeTwoStageAsync(sourcePath, sourceFileName, software144).ConfigureAwait(false);
#elif WINDOWS
        return await TranscodeTwoStageAsync(sourcePath, sourceFileName, software144).ConfigureAwait(false);
#else
        var bytes = await File.ReadAllBytesAsync(sourcePath).ConfigureAwait(false);
        return (true, bytes, sourceMime, sourceFileName, null);
#endif
    }

    internal static (int Width, int Height) FitEven(int srcW, int srcH, int maxW, int maxH)
    {
        srcW = Math.Max(srcW, 2);
        srcH = Math.Max(srcH, 2);
        maxW = Math.Max(maxW, 2) & ~1;
        maxH = Math.Max(maxH, 2) & ~1;
        var scale = Math.Min(1.0, Math.Min(maxW / (double)srcW, maxH / (double)srcH));
        var w = Math.Max(2, (int)Math.Round(srcW * scale) & ~1);
        var h = Math.Max(2, (int)Math.Round(srcH * scale) & ~1);
        return (w, h);
    }

    /// <summary>H.264 encoders often require macroblock alignment.</summary>
    internal static (int Width, int Height) AlignMacroblock(int w, int h)
    {
        w = Math.Max(16, w / 16 * 16);
        h = Math.Max(16, h / 16 * 16);
        return (w, h);
    }

#if ANDROID || WINDOWS
    private static async Task<(bool Ok, byte[]? Bytes, string Mime, string FileName, string? Error)>
        TranscodeTwoStageAsync(string sourcePath, string sourceFileName, bool software144)
    {
        var box240 = MediaEconomy.Hardware240pBox();
        var br240 = TrafficQualityMode.Economy.GetCameraVideoBitrate();
        var audio240 = MediaEconomy.SpeechBitrate(TrafficQualityMode.Economy);

        var hw = await PassAsync(sourcePath, sourceFileName, box240.Width, box240.Height, br240, audio240,
            softwareEncoder: false).ConfigureAwait(false);
        if (!hw.Ok || hw.Bytes == null || hw.Bytes.Length == 0)
            return hw;

        if (!software144)
            return hw;

        var mid = Path.Combine(FileSystem.CacheDirectory, $"iskra_vid_hw_{DateTime.UtcNow.Ticks}.mp4");
        try
        {
            await File.WriteAllBytesAsync(mid, hw.Bytes).ConfigureAwait(false);
            var box144 = MediaEconomy.Software144pBox();
            var br144 = TrafficQualityMode.UltraEconomy.GetCameraVideoBitrate();
            var audio144 = MediaEconomy.SpeechBitrate(TrafficQualityMode.UltraEconomy);
            var sw = await PassAsync(mid, hw.FileName, box144.Width, box144.Height, br144, audio144,
                softwareEncoder: true).ConfigureAwait(false);
            return sw.Ok && sw.Bytes is { Length: > 0 } ? sw : hw;
        }
        finally
        {
            TryDelete(mid);
        }
    }

    private static Task<(bool Ok, byte[]? Bytes, string Mime, string FileName, string? Error)> PassAsync(
        string sourcePath, string sourceFileName, int boxW, int boxH, int videoBitrate, int audioBitrate,
        bool softwareEncoder)
    {
#if ANDROID
        return AndroidVideoTranscoder.TranscodeAsync(
            sourcePath, sourceFileName, boxW, boxH, videoBitrate, audioBitrate, TargetFps, softwareEncoder);
#elif WINDOWS
        return TranscodeWindowsPassAsync(
            sourcePath, sourceFileName, boxW, boxH, videoBitrate, audioBitrate, softwareEncoder);
#else
        return File.ReadAllBytesAsync(sourcePath).ContinueWith(t =>
            (true, (byte[]?)t.Result, "video/mp4", sourceFileName, (string?)null));
#endif
    }
#endif

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

#if WINDOWS
    private static async Task<(bool Ok, byte[]? Bytes, string Mime, string FileName, string? Error)>
        TranscodeWindowsPassAsync(string sourcePath, string sourceFileName, int boxW, int boxH,
            int videoBitrate, int audioBitrate, bool softwareEncoder)
    {
        string? destPath = null;
        try
        {
            var src = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);
            var (srcW, srcH) = await DisplaySizeAsync(src).ConfigureAwait(false);
            var (w, h) = FitEven(srcW, srcH, boxW, boxH);
            var destName = Path.GetFileNameWithoutExtension(sourceFileName) + $"_{w}x{h}.mp4";
            destPath = Path.Combine(FileSystem.CacheDirectory, $"iskra_vid_{DateTime.UtcNow.Ticks}.mp4");
            var folder = await global::Windows.Storage.StorageFolder.GetFolderFromPathAsync(FileSystem.CacheDirectory);
            var dest = await folder.CreateFileAsync(Path.GetFileName(destPath),
                global::Windows.Storage.CreationCollisionOption.ReplaceExisting);

            var profile = global::Windows.Media.MediaProperties.MediaEncodingProfile.CreateMp4(
                global::Windows.Media.MediaProperties.VideoEncodingQuality.Wvga);
            profile.Video = global::Windows.Media.MediaProperties.VideoEncodingProperties.CreateH264();
            profile.Video.Width = (uint)w;
            profile.Video.Height = (uint)h;
            profile.Video.Bitrate = (uint)videoBitrate;
            profile.Video.FrameRate.Numerator = (uint)TargetFps;
            profile.Video.FrameRate.Denominator = 1u;
            profile.Video.ProfileId = global::Windows.Media.MediaProperties.H264ProfileIds.Baseline;
            if (profile.Audio != null)
            {
                profile.Audio.Bitrate = (uint)Math.Max(audioBitrate, 8_000);
                try
                {
                    profile.Audio.ChannelCount = 1;
                }
                catch
                {
                    // some AAC profiles reject channel changes
                }
            }

            var transcoder = new global::Windows.Media.Transcoding.MediaTranscoder();
            PreferSoftwareEncoder(transcoder, softwareEncoder);
            var prepare = await transcoder.PrepareFileTranscodeAsync(src, dest, profile);
            if (!prepare.CanTranscode)
                return (false, null, "video/mp4", destName, prepare.FailureReason.ToString());

            await prepare.TranscodeAsync();
            var bytes = await File.ReadAllBytesAsync(dest.Path).ConfigureAwait(false);
            return bytes.Length == 0
                ? (false, null, "video/mp4", destName, "Пустой результат перекодирования.")
                : (true, bytes, "video/mp4", destName, null);
        }
        catch (Exception ex)
        {
            return (false, null, "video/mp4", sourceFileName, ex.Message);
        }
        finally
        {
            if (destPath != null)
                TryDelete(destPath);
        }
    }

    private static async Task<(int Width, int Height)> DisplaySizeAsync(global::Windows.Storage.StorageFile src)
    {
        var props = await src.Properties.GetVideoPropertiesAsync();
        var w = Math.Max((int)props.Width, 2);
        var h = Math.Max((int)props.Height, 2);
        var deg = (int)props.Orientation;
        if (deg is 90 or 270)
            (w, h) = (h, w);
        return (w, h);
    }

    private static void PreferSoftwareEncoder(
        global::Windows.Media.Transcoding.MediaTranscoder transcoder, bool software)
    {
        if (!software)
            return;
        transcoder.VideoProcessingAlgorithm =
            global::Windows.Media.Transcoding.MediaVideoProcessingAlgorithm.MrfCrf444;
        var prop = transcoder.GetType().GetProperty("HardwareAccelerationEnabled");
        if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
            prop.SetValue(transcoder, false);
    }
#endif
}
