using System.Buffers.Binary;

namespace TorgLink.Maui.Services;

/// <summary>
/// Best-effort duration from local attachment bytes (no platform Media API).
/// Ogg Opus and MP4/ISO BMFF; returns null when bytes are missing or format is unknown.
/// </summary>
internal static class MediaDuration
{
    public static TimeSpan? TryGet(ReadOnlySpan<byte> bytes, string? mimeType, string? fileName)
    {
        if (bytes.IsEmpty)
            return null;

        if (LooksLikeOgg(bytes) || IsAudioMime(mimeType) || EndsWith(fileName, ".ogg", ".oga", ".opus"))
        {
            var ogg = TryOggOpus(bytes);
            if (ogg != null)
                return ogg;
        }

        if (LooksLikeMp4(bytes) || IsVideoMime(mimeType) ||
            EndsWith(fileName, ".mp4", ".m4a", ".m4v", ".mov", ".3gp"))
        {
            var mp4 = TryMp4(bytes);
            if (mp4 != null)
                return mp4;
        }

        return null;
    }

    public static string Format(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;
        var totalSeconds = (int)Math.Round(duration.TotalSeconds);
        if (totalSeconds < 0)
            totalSeconds = 0;
        var h = totalSeconds / 3600;
        var m = totalSeconds % 3600 / 60;
        var s = totalSeconds % 60;
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
    }

    private static bool IsAudioMime(string? mime) =>
        mime?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsVideoMime(string? mime) =>
        mime?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true;

    private static bool EndsWith(string? name, params string[] exts)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        foreach (var e in exts)
        {
            if (name.EndsWith(e, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool LooksLikeOgg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 4 && bytes[0] == (byte)'O' && bytes[1] == (byte)'g' && bytes[2] == (byte)'g' &&
        bytes[3] == (byte)'S';

    private static bool LooksLikeMp4(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8 &&
        ((bytes[4] == (byte)'f' && bytes[5] == (byte)'t' && bytes[6] == (byte)'y' && bytes[7] == (byte)'p') ||
         (bytes[4] == (byte)'m' && bytes[5] == (byte)'o' && bytes[6] == (byte)'o' && bytes[7] == (byte)'v') ||
         (bytes[4] == (byte)'m' && bytes[5] == (byte)'d' && bytes[6] == (byte)'a' && bytes[7] == (byte)'t'));

    /// <summary>
    /// Opus-in-Ogg: duration ≈ (last granule − pre-skip) / 48000.
    /// Granule is always in 48 kHz units per RFC 7845.
    /// </summary>
    private static TimeSpan? TryOggOpus(ReadOnlySpan<byte> bytes)
    {
        if (!LooksLikeOgg(bytes))
            return null;

        var preSkip = 0;
        long lastGranule = -1;
        var offset = 0;
        while (offset + 27 <= bytes.Length)
        {
            if (bytes[offset] != (byte)'O' || bytes[offset + 1] != (byte)'g' ||
                bytes[offset + 2] != (byte)'g' || bytes[offset + 3] != (byte)'S')
            {
                // Resync: search forward for next page (corrupt / non-Ogg prefix).
                var next = IndexOfOggS(bytes, offset + 1);
                if (next < 0)
                    break;
                offset = next;
                continue;
            }

            var granule = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(offset + 6, 8));
            var segmentCount = bytes[offset + 26];
            var headerSize = 27 + segmentCount;
            if (offset + headerSize > bytes.Length)
                break;

            var bodySize = 0;
            for (var i = 0; i < segmentCount; i++)
                bodySize += bytes[offset + 27 + i];

            var pageEnd = offset + headerSize + bodySize;
            if (pageEnd > bytes.Length)
                break;

            if (preSkip == 0 && bodySize >= 16)
            {
                var body = bytes.Slice(offset + headerSize, bodySize);
                var opusHead = IndexOfAscii(body, "OpusHead");
                if (opusHead >= 0 && opusHead + 12 <= body.Length)
                    preSkip = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(opusHead + 10, 2));
            }

            if (granule > 0)
                lastGranule = granule;

            offset = pageEnd;
        }

        if (lastGranule <= 0)
            return null;

        var samples = lastGranule - preSkip;
        if (samples <= 0)
            return null;

        return TimeSpan.FromSeconds(samples / 48_000.0);
    }

    private static TimeSpan? TryMp4(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16)
            return null;

        // Prefer moov (may be before or after mdat). Walk top-level boxes only.
        var moov = FindBox(bytes, 0, bytes.Length, "moov");
        if (moov == null)
            return null;

        var (moovStart, moovSize) = moov.Value;
        var contentStart = moovStart + 8;
        var contentEnd = moovStart + moovSize;
        if (contentEnd > bytes.Length)
            contentEnd = bytes.Length;

        var mvhd = FindBox(bytes, contentStart, (int)contentEnd, "mvhd");
        if (mvhd == null)
            return null;

        var (mvhdStart, _) = mvhd.Value;
        var body = mvhdStart + 8;
        if (body + 1 >= bytes.Length)
            return null;

        var version = bytes[body];
        switch (version)
        {
            // creation(4) modification(4) timescale(4) duration(4)
            case 0 when body + 20 > bytes.Length:
                return null;
            case 0:
            {
                var timescale = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(body + 12, 4));
                var duration = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(body + 16, 4));
                if (timescale == 0 || duration == 0)
                    return null;
                return TimeSpan.FromSeconds(duration / (double)timescale);
            }
            // creation(8) modification(8) timescale(4) duration(8)
            case 1 when body + 32 > bytes.Length:
                return null;
            case 1:
            {
                var timescale = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(body + 20, 4));
                var duration = BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(body + 24, 8));
                if (timescale == 0 || duration == 0)
                    return null;
                return TimeSpan.FromSeconds(duration / (double)timescale);
            }
            default:
                return null;
        }
    }

    private static (int Start, long Size)? FindBox(ReadOnlySpan<byte> bytes, int start, int end, string type)
    {
        var t0 = (byte)type[0];
        var t1 = (byte)type[1];
        var t2 = (byte)type[2];
        var t3 = (byte)type[3];
        var i = start;
        while (i + 8 <= end)
        {
            var size32 = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(i, 4));
            long boxSize;
            var header = 8;
            if (size32 == 1)
            {
                if (i + 16 > end)
                    return null;
                boxSize = (long)BinaryPrimitives.ReadUInt64BigEndian(bytes.Slice(i + 8, 8));
                header = 16;
            }
            else if (size32 == 0)
            {
                boxSize = end - i;
            }
            else
            {
                boxSize = size32;
            }

            if (boxSize < header || i + boxSize > end)
                return null;

            if (bytes[i + 4] == t0 && bytes[i + 5] == t1 && bytes[i + 6] == t2 && bytes[i + 7] == t3)
                return (i, boxSize);

            i += (int)boxSize;
        }

        return null;
    }

    private static int IndexOfOggS(ReadOnlySpan<byte> bytes, int from)
    {
        for (var i = from; i + 3 < bytes.Length; i++)
        {
            if (bytes[i] == (byte)'O' && bytes[i + 1] == (byte)'g' && bytes[i + 2] == (byte)'g' &&
                bytes[i + 3] == (byte)'S')
                return i;
        }

        return -1;
    }

    private static int IndexOfAscii(ReadOnlySpan<byte> haystack, string needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
            return -1;
        var n0 = (byte)needle[0];
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack[i] != n0)
                continue;
            var ok = true;
            for (var j = 1; j < needle.Length; j++)
            {
                if (haystack[i + j] != (byte)needle[j])
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
                return i;
        }

        return -1;
    }
}
