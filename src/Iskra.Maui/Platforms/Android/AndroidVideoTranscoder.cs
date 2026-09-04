using Android.Media;
using Java.Nio;

namespace Iskra.Maui.Services;

/// <summary>
/// On-device H.264/AAC transcode via MediaCodec. Hardware encoder by default;
/// softwareEncoder prefers OMX.google / c2.android software AVC.
/// </summary>
internal static class AndroidVideoTranscoder
{
    private const int TimeoutUs = 10_000;
    private const int InfoOutputFormatChanged = -2;
    private const int ColorYuv420Planar = 19;
    private const int ColorYuv420SemiPlanar = 21;
    private const int ColorYuv420Flexible = 0x7F420888;

    public static Task<(bool Ok, byte[]? Bytes, string Mime, string FileName, string? Error)> TranscodeAsync(
        string sourcePath, string sourceFileName, int boxW, int boxH, int videoBitrate, int audioBitrate,
        int fps, bool softwareEncoder)
    {
        return Task.Run(() =>
            Transcode(sourcePath, sourceFileName, boxW, boxH, videoBitrate, audioBitrate, fps, softwareEncoder));
    }

    private static (bool Ok, byte[]? Bytes, string Mime, string FileName, string? Error) Transcode(
        string sourcePath, string sourceFileName, int boxW, int boxH, int videoBitrate, int audioBitrate,
        int fps, bool softwareEncoder)
    {
        _ = audioBitrate;
        var destName = Path.GetFileNameWithoutExtension(sourceFileName) + ".mp4";
        var destPath = Path.Combine(FileSystem.CacheDirectory, $"iskra_and_{DateTime.UtcNow.Ticks}.mp4");
        MediaExtractor? extractor = null;
        MediaCodec? decoder = null;
        MediaCodec? encoder = null;
        MediaMuxer? muxer = null;
        try
        {
            extractor = new MediaExtractor();
            extractor.SetDataSource(sourcePath);

            var vTrack = FindTrack(extractor, "video/");
            if (vTrack < 0)
                return (false, null, "video/mp4", destName, "Нет видеодорожки.");

            var inFmt = extractor.GetTrackFormat(vTrack)!;
            var inMime = inFmt.GetString(MediaFormat.KeyMime) ?? "video/avc";
            var srcW = inFmt.GetInteger(MediaFormat.KeyWidth);
            var srcH = inFmt.GetInteger(MediaFormat.KeyHeight);
            var rotation = ReadRotation(inFmt);
            if (rotation is 90 or 270)
                (srcW, srcH) = (srcH, srcW);

            var fitted = Video144pTranscoder.FitEven(srcW, srcH, boxW, boxH);
            var (outW, outH) = Video144pTranscoder.AlignMacroblock(fitted.Width, fitted.Height);
            destName = Path.GetFileNameWithoutExtension(sourceFileName) + $"_{outW}x{outH}.mp4";

            decoder = MediaCodec.CreateDecoderByType(inMime);
            decoder.Configure(inFmt, null, null, 0);
            decoder.Start();

            encoder = CreateAvcEncoder(softwareEncoder);
            var encFmt = MediaFormat.CreateVideoFormat(MediaFormat.MimetypeVideoAvc, outW, outH);
            encFmt.SetInteger(MediaFormat.KeyBitRate, Math.Max(videoBitrate, 32_000));
            encFmt.SetInteger(MediaFormat.KeyFrameRate, Math.Max(fps, 8));
            encFmt.SetInteger(MediaFormat.KeyIFrameInterval, 2);
            encFmt.SetInteger(MediaFormat.KeyColorFormat, ColorYuv420SemiPlanar);
            try
            {
                encoder.Configure(encFmt, null, null, MediaCodecConfigFlags.Encode);
            }
            catch
            {
                encFmt.SetInteger(MediaFormat.KeyColorFormat, ColorYuv420Planar);
                encoder.Configure(encFmt, null, null, MediaCodecConfigFlags.Encode);
            }

            encoder.Start();
            var encColor = ColorYuv420SemiPlanar;
            try
            {
                encColor = encoder.InputFormat.GetInteger(MediaFormat.KeyColorFormat);
            }
            catch
            {
                // NV12 default
            }

            muxer = new MediaMuxer(destPath, MuxerOutputType.Mpeg4);
            extractor.SelectTrack(vTrack);

            var videoTrackIx = -1;
            var audioTrackIx = -1;
            var muxerStarted = false;
            var decInEos = false;
            var encInEos = false;
            var encOutEos = false;
            var info = new MediaCodec.BufferInfo();
            var pendingI420 = new Queue<(byte[] Yuv, long Pts)>();

            while (!encOutEos)
            {
                if (!decInEos)
                    decInEos = FeedExtractor(extractor, decoder);

                DrainDecoder(decoder, info, srcW, srcH, rotation, outW, outH, pendingI420);

                if (!encInEos)
                    encInEos = FeedEncoder(encoder, pendingI420, encColor, outW, outH, decInEos);

                DrainEncoder(encoder, info, muxer, sourcePath, ref videoTrackIx, ref audioTrackIx,
                    ref muxerStarted, ref encOutEos);
            }

            if (muxerStarted && audioTrackIx >= 0)
                CopyAudio(sourcePath, muxer, audioTrackIx);

            muxer.Stop();
            muxer.Release();
            muxer = null;

            var bytes = File.ReadAllBytes(destPath);
            return bytes.Length == 0
                ? (false, null, "video/mp4", destName, "Пустой результат перекодирования.")
                : (true, bytes, "video/mp4", destName, null);
        }
        catch (Exception ex)
        {
            return (false, null, "video/mp4", destName, ex.Message);
        }
        finally
        {
            TryStop(decoder);
            TryStop(encoder);
            decoder?.Release();
            encoder?.Release();
            extractor?.Release();
            try
            {
                muxer?.Release();
            }
            catch
            {
                // ignore
            }

            try
            {
                if (File.Exists(destPath))
                    File.Delete(destPath);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static int FindTrack(MediaExtractor extractor, string prefix)
    {
        for (var i = 0; i < extractor.TrackCount; i++)
        {
            var mime = extractor.GetTrackFormat(i)?.GetString(MediaFormat.KeyMime);
            if (mime != null && mime.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static int ReadRotation(MediaFormat format)
    {
        try
        {
            if (format.ContainsKey(MediaFormat.KeyRotation))
                return format.GetInteger(MediaFormat.KeyRotation);
        }
        catch
        {
            // ignore
        }

        try
        {
            if (format.ContainsKey("rotation-degrees"))
                return format.GetInteger("rotation-degrees");
        }
        catch
        {
            // ignore
        }

        return 0;
    }

    private static MediaCodec CreateAvcEncoder(bool software)
    {
        if (software)
        {
            foreach (var name in new[] { "c2.android.avc.encoder", "OMX.google.h264.encoder" })
            {
                try
                {
                    var c = MediaCodec.CreateByCodecName(name);
                    if (c != null)
                        return c;
                }
                catch
                {
                    // try next
                }
            }
        }

        return MediaCodec.CreateEncoderByType(MediaFormat.MimetypeVideoAvc)!;
    }

    private static bool FeedExtractor(MediaExtractor extractor, MediaCodec decoder)
    {
        var ix = decoder.DequeueInputBuffer(TimeoutUs);
        if (ix < 0)
            return false;
        var buf = decoder.GetInputBuffer(ix);
        if (buf == null)
            return false;
        var sample = extractor.ReadSampleData(buf, 0);
        if (sample < 0)
        {
            decoder.QueueInputBuffer(ix, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
            return true;
        }

        decoder.QueueInputBuffer(ix, 0, sample, extractor.SampleTime, 0);
        extractor.Advance();
        return false;
    }

    private static void DrainDecoder(
        MediaCodec decoder, MediaCodec.BufferInfo info, int srcW, int srcH, int rotation,
        int outW, int outH, Queue<(byte[] Yuv, long Pts)> pending)
    {
        var ix = decoder.DequeueOutputBuffer(info, TimeoutUs);
        if (ix < 0)
            return;
        try
        {
            if ((info.Flags & MediaCodecBufferFlags.EndOfStream) != 0 && info.Size <= 0)
                return;
            var buf = decoder.GetOutputBuffer(ix);
            if (buf == null || info.Size <= 0)
                return;
            var outFmt = decoder.OutputFormat;
            var color = outFmt.ContainsKey(MediaFormat.KeyColorFormat)
                ? outFmt.GetInteger(MediaFormat.KeyColorFormat)
                : ColorYuv420Flexible;
            var codedW = outFmt.ContainsKey(MediaFormat.KeyWidth) ? outFmt.GetInteger(MediaFormat.KeyWidth) : srcW;
            var codedH = outFmt.ContainsKey(MediaFormat.KeyHeight) ? outFmt.GetInteger(MediaFormat.KeyHeight) : srcH;
            var stride = outFmt.ContainsKey(MediaFormat.KeyStride) ? outFmt.GetInteger(MediaFormat.KeyStride) : codedW;
            var slice = outFmt.ContainsKey(MediaFormat.KeySliceHeight)
                ? outFmt.GetInteger(MediaFormat.KeySliceHeight)
                : codedH;
            if (stride <= 0)
                stride = codedW;
            if (slice <= 0)
                slice = codedH;

            var raw = new byte[info.Size];
            buf.Position(info.Offset);
            buf.Get(raw, 0, info.Size);
            var i420 = ToI420(raw, color, codedW, codedH, stride, slice);
            var upright = RotateI420(i420, codedW, codedH, rotation);
            var uw = rotation is 90 or 270 ? codedH : codedW;
            var uh = rotation is 90 or 270 ? codedW : codedH;
            var scaled = ScaleI420(upright, uw, uh, outW, outH);
            pending.Enqueue((scaled, info.PresentationTimeUs));
        }
        finally
        {
            decoder.ReleaseOutputBuffer(ix, false);
        }
    }

    private static bool FeedEncoder(
        MediaCodec encoder, Queue<(byte[] Yuv, long Pts)> pending, int encColor, int w, int h, bool decoderEos)
    {
        if (pending.Count == 0)
        {
            if (!decoderEos)
                return false;
            var eosIx = encoder.DequeueInputBuffer(TimeoutUs);
            if (eosIx < 0)
                return false;
            encoder.QueueInputBuffer(eosIx, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
            return true;
        }

        var ix = encoder.DequeueInputBuffer(TimeoutUs);
        if (ix < 0)
            return false;
        var (yuv, pts) = pending.Dequeue();
        var packed = FromI420(yuv, encColor, w, h);
        var buf = encoder.GetInputBuffer(ix);
        if (buf == null)
            return false;
        buf.Clear();
        var n = Math.Min(packed.Length, buf.Remaining());
        buf.Put(packed, 0, n);
        encoder.QueueInputBuffer(ix, 0, n, pts, 0);
        return false;
    }

    private static void DrainEncoder(
        MediaCodec encoder, MediaCodec.BufferInfo info, MediaMuxer muxer, string sourcePath,
        ref int videoTrackIx, ref int audioTrackIx, ref bool muxerStarted, ref bool encOutEos)
    {
        var ix = encoder.DequeueOutputBuffer(info, TimeoutUs);
        if (ix == InfoOutputFormatChanged)
        {
            if (videoTrackIx < 0)
                videoTrackIx = muxer.AddTrack(encoder.OutputFormat);
            return;
        }

        if (ix < 0)
            return;

        try
        {
            if ((info.Flags & MediaCodecBufferFlags.CodecConfig) != 0)
                info.Size = 0;

            if (info.Size > 0)
            {
                if (!muxerStarted)
                {
                    if (videoTrackIx < 0)
                        videoTrackIx = muxer.AddTrack(encoder.OutputFormat);
                    if (audioTrackIx < 0)
                        audioTrackIx = TryAddAudioTrack(muxer, sourcePath);
                    muxer.Start();
                    muxerStarted = true;
                }

                var buf = encoder.GetOutputBuffer(ix);
                if (buf != null)
                {
                    buf.Position(info.Offset);
                    buf.Limit(info.Offset + info.Size);
                    muxer.WriteSampleData(videoTrackIx, buf, info);
                }
            }

            if ((info.Flags & MediaCodecBufferFlags.EndOfStream) != 0)
                encOutEos = true;
        }
        finally
        {
            encoder.ReleaseOutputBuffer(ix, false);
        }
    }

    private static int TryAddAudioTrack(MediaMuxer muxer, string sourcePath)
    {
        MediaExtractor? src = null;
        try
        {
            src = new MediaExtractor();
            src.SetDataSource(sourcePath);
            var audioTrack = FindTrack(src, "audio/");
            if (audioTrack < 0)
                return -1;
            var fmt = src.GetTrackFormat(audioTrack);
            var mime = fmt?.GetString(MediaFormat.KeyMime) ?? "";
            if (!mime.Equals(MediaFormat.MimetypeAudioAac, StringComparison.OrdinalIgnoreCase) &&
                !mime.StartsWith("audio/mp4a", StringComparison.OrdinalIgnoreCase))
                return -1;
            return muxer.AddTrack(fmt);
        }
        catch
        {
            return -1;
        }
        finally
        {
            src?.Release();
        }
    }

    private static void CopyAudio(string sourcePath, MediaMuxer muxer, int muxTrack)
    {
        MediaExtractor? audio = null;
        try
        {
            audio = new MediaExtractor();
            audio.SetDataSource(sourcePath);
            var audioTrack = FindTrack(audio, "audio/");
            if (audioTrack < 0)
                return;
            audio.SelectTrack(audioTrack);
            var buf = ByteBuffer.Allocate(256 * 1024);
            var info = new MediaCodec.BufferInfo();
            while (true)
            {
                buf.Clear();
                var n = audio.ReadSampleData(buf, 0);
                if (n < 0)
                    break;
                info.Offset = 0;
                info.Size = n;
                info.PresentationTimeUs = audio.SampleTime;
                info.Flags = 0;
                buf.Position(0);
                buf.Limit(n);
                muxer.WriteSampleData(muxTrack, buf, info);
                audio.Advance();
            }
        }
        catch
        {
            // video-only is acceptable
        }
        finally
        {
            audio?.Release();
        }
    }

    private static byte[] ToI420(byte[] src, int color, int w, int h, int stride, int slice)
    {
        var ySize = w * h;
        var dst = new byte[ySize + ySize / 2];
        if (color == ColorYuv420SemiPlanar || color == 0x7FA30C04)
        {
            for (var y = 0; y < h; y++)
                Array.Copy(src, y * stride, dst, y * w, Math.Min(w, Math.Max(0, src.Length - y * stride)));
            var uvOff = stride * slice;
            var du = ySize;
            var dv = ySize + ySize / 4;
            for (var y = 0; y < h / 2; y++)
            {
                var row = uvOff + y * stride;
                for (var x = 0; x < w / 2; x++)
                {
                    var p = row + x * 2;
                    if (p + 1 >= src.Length)
                        break;
                    dst[du + y * (w / 2) + x] = src[p];
                    dst[dv + y * (w / 2) + x] = src[p + 1];
                }
            }

            return dst;
        }

        for (var y = 0; y < h; y++)
        {
            var off = y * stride;
            if (off >= src.Length)
                break;
            Array.Copy(src, off, dst, y * w, Math.Min(w, src.Length - off));
        }

        var uOff = stride * slice;
        var vOff = uOff + (stride / 2) * (slice / 2);
        if (vOff + (w / 2) * (h / 2) > src.Length)
        {
            uOff = ySize;
            Array.Copy(src, uOff, dst, ySize, Math.Min(ySize / 2, Math.Max(0, src.Length - uOff)));
            return dst;
        }

        for (var y = 0; y < h / 2; y++)
        {
            Array.Copy(src, uOff + y * (stride / 2), dst, ySize + y * (w / 2), w / 2);
            Array.Copy(src, vOff + y * (stride / 2), dst, ySize + ySize / 4 + y * (w / 2), w / 2);
        }

        return dst;
    }

    private static byte[] FromI420(byte[] i420, int color, int w, int h)
    {
        var ySize = w * h;
        if (color == ColorYuv420Planar)
            return i420;

        var dst = new byte[ySize + ySize / 2];
        Array.Copy(i420, 0, dst, 0, ySize);
        var du = ySize;
        var dv = ySize + ySize / 4;
        for (var y = 0; y < h / 2; y++)
        {
            for (var x = 0; x < w / 2; x++)
            {
                var i = y * (w / 2) + x;
                dst[ySize + i * 2] = i420[du + i];
                dst[ySize + i * 2 + 1] = i420[dv + i];
            }
        }

        return dst;
    }

    private static byte[] RotateI420(byte[] src, int w, int h, int rotation)
    {
        rotation = ((rotation % 360) + 360) % 360;
        if (rotation == 0)
            return src;
        var ySize = w * h;
        var dstW = rotation is 90 or 270 ? h : w;
        var dstH = rotation is 90 or 270 ? w : h;
        var dst = new byte[dstW * dstH + dstW * dstH / 2];
        RotatePlane(src, 0, w, h, dst, 0, dstW, dstH, rotation);
        RotatePlane(src, ySize, w / 2, h / 2, dst, dstW * dstH, dstW / 2, dstH / 2, rotation);
        RotatePlane(src, ySize + ySize / 4, w / 2, h / 2, dst, dstW * dstH + dstW * dstH / 4, dstW / 2, dstH / 2,
            rotation);
        return dst;
    }

    private static void RotatePlane(byte[] src, int srcOff, int sw, int sh, byte[] dst, int dstOff, int dw, int dh,
        int rotation)
    {
        _ = dh;
        for (var y = 0; y < sh; y++)
        {
            for (var x = 0; x < sw; x++)
            {
                int dx, dy;
                switch (rotation)
                {
                    case 90:
                        dx = sh - 1 - y;
                        dy = x;
                        break;
                    case 180:
                        dx = sw - 1 - x;
                        dy = sh - 1 - y;
                        break;
                    default:
                        dx = y;
                        dy = sw - 1 - x;
                        break;
                }

                dst[dstOff + dy * dw + dx] = src[srcOff + y * sw + x];
            }
        }
    }

    private static byte[] ScaleI420(byte[] src, int sw, int sh, int dw, int dh)
    {
        if (sw == dw && sh == dh)
            return src;
        var dst = new byte[dw * dh + dw * dh / 2];
        ScalePlane(src, 0, sw, sh, dst, 0, dw, dh);
        ScalePlane(src, sw * sh, sw / 2, sh / 2, dst, dw * dh, dw / 2, dh / 2);
        ScalePlane(src, sw * sh + sw * sh / 4, sw / 2, sh / 2, dst, dw * dh + dw * dh / 4, dw / 2, dh / 2);
        return dst;
    }

    private static void ScalePlane(byte[] src, int srcOff, int sw, int sh, byte[] dst, int dstOff, int dw, int dh)
    {
        for (var y = 0; y < dh; y++)
        {
            var sy = Math.Min(sh - 1, y * sh / dh);
            for (var x = 0; x < dw; x++)
            {
                var sx = Math.Min(sw - 1, x * sw / dw);
                dst[dstOff + y * dw + x] = src[srcOff + sy * sw + sx];
            }
        }
    }

    private static void TryStop(MediaCodec? codec)
    {
        try
        {
            codec?.Stop();
        }
        catch
        {
            // ignore
        }
    }
}
