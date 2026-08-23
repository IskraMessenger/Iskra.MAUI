namespace Iskra.Maui.Services;

internal static class Video144pTranscoder
{
    public static bool IsVideoMime(string mime) =>
        mime.StartsWith("video/", StringComparison.OrdinalIgnoreCase);

    public static async Task<(bool Ok, byte[]? Bytes, string Mime, string FileName, string? Error)> PrepareAsync(
        string sourcePath, string sourceFileName, string sourceMime, bool economy)
    {
        if (!economy)
        {
            var raw = await File.ReadAllBytesAsync(sourcePath).ConfigureAwait(false);
            return (true, raw, sourceMime, sourceFileName, null);
        }

#if WINDOWS
        return await TranscodeWindowsAsync(sourcePath, sourceFileName).ConfigureAwait(false);
#else
        var bytes = await File.ReadAllBytesAsync(sourcePath).ConfigureAwait(false);
        return (true, bytes, sourceMime, sourceFileName, null);
#endif
    }

#if WINDOWS
    private static async Task<(bool Ok, byte[]? Bytes, string Mime, string FileName, string? Error)>
        TranscodeWindowsAsync(string sourcePath, string sourceFileName)
    {
        try
        {
            var destName = Path.GetFileNameWithoutExtension(sourceFileName) + "_144p.mp4";
            var destPath = Path.Combine(FileSystem.CacheDirectory, $"iskra_vid_{DateTime.UtcNow.Ticks}.mp4");
            var src = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(sourcePath);
            var folder = await global::Windows.Storage.StorageFolder.GetFolderFromPathAsync(FileSystem.CacheDirectory);
            var dest = await folder.CreateFileAsync(Path.GetFileName(destPath),
                global::Windows.Storage.CreationCollisionOption.ReplaceExisting);

            var profile = global::Windows.Media.MediaProperties.MediaEncodingProfile.CreateMp4(
                global::Windows.Media.MediaProperties.VideoEncodingQuality.Wvga);
            profile.Video = global::Windows.Media.MediaProperties.VideoEncodingProperties.CreateH264();
            profile.Video.Width = MediaEconomy.VideoWidth;
            profile.Video.Height = MediaEconomy.VideoHeight;
            profile.Video.Bitrate = MediaEconomy.VideoBitrateBps;
            profile.Video.FrameRate.Numerator = 15;
            profile.Video.FrameRate.Denominator = 1;
            if (profile.Audio != null)
                profile.Audio.Bitrate = 16_000;

            var transcoder = new global::Windows.Media.Transcoding.MediaTranscoder();
            var prepare = await transcoder.PrepareFileTranscodeAsync(src, dest, profile);
            if (!prepare.CanTranscode)
                return (false, null, "video/mp4", destName,
                    prepare.FailureReason.ToString());

            await prepare.TranscodeAsync();
            var bytes = await File.ReadAllBytesAsync(dest.Path).ConfigureAwait(false);
            try
            {
                File.Delete(dest.Path);
            }
            catch
            {
                // ignore
            }

            return bytes.Length == 0
                ? (false, null, "video/mp4", destName, "Пустой результат перекодирования.")
                : (true, bytes, "video/mp4", destName, null);
        }
        catch (Exception ex)
        {
            return (false, null, "video/mp4", sourceFileName, ex.Message);
        }
    }
#endif
}
