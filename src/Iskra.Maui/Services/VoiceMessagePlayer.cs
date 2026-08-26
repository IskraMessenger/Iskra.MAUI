using Concentus;
using Concentus.Oggfile;
using Microsoft.Extensions.Logging;

namespace Iskra.Maui.Services;

/// <summary>In-app playback for downloaded voice (.ogg / Opus) attachments.</summary>
internal static class VoiceMessagePlayer
{
    private const int OpusDecodeSampleRate = 48_000;

#if WINDOWS
    private static global::Windows.Media.Playback.MediaPlayer? _windowsPlayer;
#elif ANDROID
    private static Android.Media.MediaPlayer? _androidPlayer;
#endif

    public static async Task PlayAsync(byte[] oggBytes, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(oggBytes);
        if (oggBytes.Length == 0)
            throw new InvalidOperationException("Пустое голосовое сообщение.");

        try
        {
#if WINDOWS
            await PlayWindowsAsync(oggBytes).ConfigureAwait(true);
#elif ANDROID
            await PlayAndroidAsync(oggBytes).ConfigureAwait(true);
#else
            await Task.CompletedTask.ConfigureAwait(false);
            throw new PlatformNotSupportedException("Воспроизведение голосовых на этой платформе не поддерживается.");
#endif
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Voice playback failed");
            throw;
        }
    }

    public static void Stop()
    {
#if WINDOWS
        try
        {
            _windowsPlayer?.Pause();
            _windowsPlayer?.Dispose();
        }
        catch
        {
            // ignore
        }

        _windowsPlayer = null;
#elif ANDROID
        try
        {
            _androidPlayer?.Stop();
            _androidPlayer?.Release();
            _androidPlayer?.Dispose();
        }
        catch
        {
            // ignore
        }

        _androidPlayer = null;
#endif
    }

#if WINDOWS
    private static async Task PlayWindowsAsync(byte[] oggBytes)
    {
        Stop();
        // Prefer WAV from Opus decode — Windows MediaPlayer often cannot play Opus/Ogg.
        var wavPath = Path.Combine(FileSystem.CacheDirectory, $"voice_play_{Guid.NewGuid():N}.wav");
        try
        {
            var (pcm, sampleRate) = DecodeOpusOggToPcm16(oggBytes);
            await File.WriteAllBytesAsync(wavPath, BuildWav(pcm, sampleRate)).ConfigureAwait(true);
            var file = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(wavPath);
            var player = new global::Windows.Media.Playback.MediaPlayer();
            _windowsPlayer = player;
            player.MediaEnded += (_, _) =>
            {
                try
                {
                    player.Dispose();
                }
                catch
                {
                    // ignore
                }

                if (ReferenceEquals(_windowsPlayer, player))
                    _windowsPlayer = null;
                TryDelete(wavPath);
            };
            player.Source = global::Windows.Media.Core.MediaSource.CreateFromStorageFile(file);
            player.Play();
        }
        catch
        {
            TryDelete(wavPath);
            throw;
        }
    }
#elif ANDROID
    private static async Task PlayAndroidAsync(byte[] oggBytes)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"voice_play_{Guid.NewGuid():N}.ogg");
        await File.WriteAllBytesAsync(path, oggBytes).ConfigureAwait(true);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Stop();
            var player = new Android.Media.MediaPlayer();
            _androidPlayer = player;
            var attrs = new Android.Media.AudioAttributes.Builder()
                .SetUsage(Android.Media.AudioUsageKind.Media)
                .SetContentType(Android.Media.AudioContentType.Speech)
                .Build();
            if (attrs != null)
                player.SetAudioAttributes(attrs);
            player.SetDataSource(path);
            player.Prepare();
            player.Completion += (_, _) =>
            {
                try
                {
                    player.Release();
                    player.Dispose();
                }
                catch
                {
                    // ignore
                }

                if (ReferenceEquals(_androidPlayer, player))
                    _androidPlayer = null;
                TryDelete(path);
            };
            player.Error += (_, _) =>
            {
                try
                {
                    player.Release();
                    player.Dispose();
                }
                catch
                {
                    // ignore
                }

                if (ReferenceEquals(_androidPlayer, player))
                    _androidPlayer = null;
                TryDelete(path);
            };
            player.Start();
        }).ConfigureAwait(true);
    }
#endif

    private static (byte[] PcmBytes, int SampleRateHz) DecodeOpusOggToPcm16(byte[] oggBytes)
    {
        using var mem = new MemoryStream(oggBytes, false);
        var decoder = OpusCodecFactory.CreateDecoder(OpusDecodeSampleRate, 1);
        var oggIn = new OpusOggReadStream(decoder, mem);
        var samples = new List<short>();
        while (oggIn.HasNextPacket)
        {
            var pkt = oggIn.DecodeNextPacket();
            if (pkt is { Length: > 0 })
                samples.AddRange(pkt);
        }

        if (samples.Count == 0)
            throw new InvalidOperationException("Пустой Opus поток.");

        var pcm = new byte[samples.Count * 2];
        Buffer.BlockCopy(samples.ToArray(), 0, pcm, 0, pcm.Length);
        return (pcm, OpusDecodeSampleRate);
    }

    private static byte[] BuildWav(byte[] pcm16, int sampleRateHz)
    {
        using var ms = new MemoryStream(44 + pcm16.Length);
        using var bw = new BinaryWriter(ms);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + pcm16.Length);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)1);
        bw.Write(sampleRateHz);
        bw.Write(sampleRateHz * 2);
        bw.Write((short)2);
        bw.Write((short)16);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(pcm16.Length);
        bw.Write(pcm16);
        return ms.ToArray();
    }

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
}
