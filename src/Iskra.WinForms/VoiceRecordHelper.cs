using Concentus;
using Concentus.Enums;
using Concentus.Oggfile;
using NAudio.Wave;

namespace Iskra.WinForms;

/// <summary>Голосовые сообщения: Ogg + Opus, моно (как ShortP2P.WinForms / MAUI).</summary>
internal static class VoiceRecordHelper
{
    public const string VoiceMessageMime = "audio/ogg";
    public const string VoiceFileName = "voice.ogg";

    private const int OpusDecodeSampleRate = 48000;

    /// <summary>WAV (RIFF) в памяти → Ogg Opus mono at the given bitrate (bps).</summary>
    public static Task<(bool Ok, byte[]? OggBytes, string? Error)> EncodeWavPcmToOggOpusAsync(
        byte[] wavBytes, int bitrateBps, CancellationToken cancellationToken = default) =>
        Task.Run(() => EncodeWavPcmToOggOpus(wavBytes, bitrateBps), cancellationToken);

    private static (bool Ok, byte[]? OggBytes, string? Error) EncodeWavPcmToOggOpus(
        byte[] wavBytes, int bitrateBps)
    {
        if (wavBytes.Length < 44)
            return (false, null, "Слишком короткая запись.");

        try
        {
            using var wavMs = new MemoryStream(wavBytes, false);
            using var wav = new WaveFileReader(wavMs);
            var fmt = wav.WaveFormat;
            if (fmt.BitsPerSample != 16)
                return (false, null, "Ожидается PCM 16 bit в WAV.");

            var sampleRate = fmt.SampleRate;
            var channels = fmt.Channels;
            var byteBuffer = new byte[wav.Length];
            var pos = 0;
            int read;
            while ((read = wav.Read(byteBuffer, pos, byteBuffer.Length - pos)) > 0)
                pos += read;
            if (pos != byteBuffer.Length)
                Array.Resize(ref byteBuffer, pos);

            var frameCount = byteBuffer.Length / (2 * channels);
            if (frameCount == 0)
                return (false, null, "Нет аудиосэмплов.");

            var interleavedShorts = new short[frameCount * channels];
            Buffer.BlockCopy(byteBuffer, 0, interleavedShorts, 0, byteBuffer.Length);

            short[] monoPcm;
            if (channels == 1)
            {
                monoPcm = new short[frameCount];
                Array.Copy(interleavedShorts, monoPcm, frameCount);
            }
            else
            {
                monoPcm = new short[frameCount];
                for (var i = 0; i < frameCount; i++)
                {
                    var sum = 0;
                    for (var c = 0; c < channels; c++)
                        sum += interleavedShorts[i * channels + c];
                    monoPcm[i] = (short)(sum / channels);
                }
            }

            var clampedBitrate = Math.Min(64_000, Math.Max(MediaEconomy.MinVoiceBitrateBps, bitrateBps));
            var oggMs = new MemoryStream();
            var encoder =
                OpusCodecFactory.CreateEncoder(OpusDecodeSampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
            encoder.Bitrate = clampedBitrate;
            var tags = new OpusTags();
            var oggOut = new OpusOggWriteStream(encoder, oggMs, tags, sampleRate);
            oggOut.WriteSamples(monoPcm, 0, monoPcm.Length);
            oggOut.Finish();

            var ogg = oggMs.ToArray();
            return ogg.Length == 0 ? (false, null, "Пустой выход кодера.") : (true, ogg, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }
}
