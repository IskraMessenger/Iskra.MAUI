using System.Diagnostics.CodeAnalysis;
using ShortP2P.Auth.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Img = SixLabors.ImageSharp.Image;

namespace TorgLink.Maui;

/// <summary>
/// Prepares profile avatars: center square crop (for circular UI clip), scale to 512×512, optional JPEG compress ≤ limit.
/// </summary>
internal static class AvatarImageProcessor
{
    public const int Dimension = 512;

    public enum PrepareStatus
    {
        Ok,
        NeedsCompression,
        Failed
    }

    public readonly record struct PrepareResult(
        PrepareStatus Status,
        byte[]? Bytes,
        int SizeAfterCrop,
        string? Error);

    /// <summary>
    /// Crops/scales to 512×512 and encodes at high quality. If still over <paramref name="maxBytes"/>,
    /// returns <see cref="PrepareStatus.NeedsCompression"/> with the oversized bytes for a consent prompt.
    /// </summary>
    public static PrepareResult PrepareCropped(ReadOnlySpan<byte> source, int maxBytes = PeerProfileLimits.MaxAvatarBytes)
    {
        if (maxBytes < 4096)
            return new PrepareResult(PrepareStatus.Failed, null, 0, "Max size is too small.");

        try
        {
            using var loaded = Img.Load(source);
            var side = Math.Min(loaded.Width, loaded.Height);
            if (side < 1)
                return new PrepareResult(PrepareStatus.Failed, null, 0, "Invalid image.");

            var cropX = (loaded.Width - side) / 2;
            var cropY = (loaded.Height - side) / 2;
            loaded.Mutate(x =>
            {
                x.Crop(new Rectangle(cropX, cropY, side, side));
                x.Resize(Dimension, Dimension);
            });

            using var ms = new MemoryStream();
            loaded.SaveAsJpeg(ms, new JpegEncoder { Quality = 90 });
            var bytes = ms.ToArray();
            if (bytes.Length <= maxBytes)
                return new PrepareResult(PrepareStatus.Ok, bytes, bytes.Length, null);

            return new PrepareResult(PrepareStatus.NeedsCompression, bytes, bytes.Length, null);
        }
        catch (Exception ex)
        {
            return new PrepareResult(PrepareStatus.Failed, null, 0, ex.Message);
        }
    }

    /// <summary>Compresses an already cropped 512×512 image (or any bytes) down to <paramref name="maxBytes"/>.</summary>
    public static bool TryCompressToLimit(ReadOnlySpan<byte> source, int maxBytes,
        [NotNullWhen(true)] out byte[]? output,
        [NotNullWhen(false)] out string? error)
    {
        output = null;
        error = null;
        if (maxBytes < 4096)
        {
            error = "Max size is too small.";
            return false;
        }

        try
        {
            using var loaded = Img.Load(source);
            for (var attempt = 0; attempt < 45; attempt++)
            {
                var q = Math.Clamp(88 - attempt, 22, 88);
                var scale = attempt < 12 ? 1.0f : MathF.Pow(0.92f, attempt - 11);
                using var work = loaded.Clone(x =>
                {
                    if (scale < 0.999f)
                    {
                        var w = Math.Max(48, (int)(Dimension * scale));
                        var h = Math.Max(48, (int)(Dimension * scale));
                        x.Resize(w, h);
                    }
                });
                using var ms = new MemoryStream();
                work.SaveAsJpeg(ms, new JpegEncoder { Quality = q });
                var bytes = ms.ToArray();
                if (bytes.Length <= maxBytes)
                {
                    output = bytes;
                    return true;
                }
            }

            error = $"Cannot fit avatar into {maxBytes / 1024} KB.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
