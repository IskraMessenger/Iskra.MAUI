using Microsoft.Extensions.Logging;
using NAudio.Wave;
using ShortP2P.Client.ChatMedia;
using ShortP2P.Discovery;
using Timer = System.Windows.Forms.Timer;

namespace Iskra.WinForms;

public sealed partial class ChatForm
{
    private const int MaxVoiceRecordSeconds = 120;

    private readonly object _voiceCapLock = new();
    private volatile bool _voiceDiscardNextStop;
    private DateTime _voiceRecordStartUtc;
    private Timer? _voiceRecordTimer;
    private WaveInEvent? _voiceWaveIn;
    private MemoryStream? _voiceWaveMs;
    private WaveFileWriter? _voiceWaveWriter;

    private async Task OnAttachImageAsync()
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;
        EnsureSession(user);
        if (_p2pSession == null)
            return;

        using var dlg = new OpenFileDialog
        {
            Title = "Изображение (JPEG, PNG, GIF)",
            Filter = "Изображения|*.jpg;*.jpeg;*.png;*.gif|Все файлы|*.*",
            CheckFileExists = true,
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;

        foreach (var path in dlg.FileNames)
            await SendPickedImageAsync(path).ConfigureAwait(true);
    }

    private async Task SendPickedImageAsync(string path)
    {
        try
        {
            if (!ImageAttachHelper.TryGetMimeFromExtension(path, out var mime))
            {
                MessageBox.Show(this, "Допустимы только .jpg, .jpeg, .png, .gif.", "Файл",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var bytes = await ReadAllBytesAsync(path).ConfigureAwait(true);
            if (bytes.Length < 12)
            {
                MessageBox.Show(this, "Файл слишком маленький.", "Файл",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!ImageAttachHelper.SniffMatchesMime(bytes.AsSpan(0, Math.Min(12, bytes.Length)), mime))
            {
                MessageBox.Show(this, "Содержимое не совпадает с расширением файла.", "Файл",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var fitted = FitOutgoingImage(bytes, mime);
            if (fitted.Bytes == null)
            {
                MessageBox.Show(this, fitted.Error ?? "Не удалось сжать изображение.", "Сжатие",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _media.ValidateMime(fitted.Mime);
            await _p2pSession!.SendImageAsync(fitted.Bytes, fitted.Mime).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send image failed in chat {ChatId}", _chat.Id);
            ShowSendError(ex.Message);
        }
        finally
        {
            await ReloadAsync().ConfigureAwait(true);
        }
    }

    private (byte[]? Bytes, string Mime, string? Error) FitOutgoingImage(byte[] bytes, string mime)
    {
        var limit = MediaEconomy.ImageLimit(_media, _routing);
        if (bytes.Length <= limit)
            return (bytes, mime, null);

        if (!ImageAttachmentCompressor.TryCompressToMaxBytes(bytes, limit, out var compressed, out var err))
            return (null, mime, err ?? "Не удалось уложиться в лимит.");

        return (compressed, ImageAttachmentCompressor.SuggestMimeAfterCompression(), null);
    }

    private async Task OnAttachDocumentAsync()
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;
        EnsureSession(user);
        if (_p2pSession == null)
            return;

        using var dlg = new OpenFileDialog
        {
            Title = "Документ Word / LibreOffice / PDF",
            Filter =
                "Документы|*.doc;*.docx;*.rtf;*.pdf;*.odt;*.ods;*.odp;*.odg;*.xlsx;*.xls;*.pptx;*.ppt|Все файлы|*.*",
            CheckFileExists = true,
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;

        foreach (var path in dlg.FileNames)
            await SendPickedDocumentAsync(path).ConfigureAwait(true);
    }

    private async Task SendPickedDocumentAsync(string path)
    {
        try
        {
            if (!DocumentAttachHelper.TryGetMimeFromExtension(path, out var mime))
            {
                MessageBox.Show(this,
                    "Допустимы только .doc, .docx, .rtf, .pdf, .odt, .ods, .odp, .odg, .xlsx, .xls, .pptx, .ppt.",
                    "Файл", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var bytes = await ReadAllBytesAsync(path).ConfigureAwait(true);
            if (bytes.Length == 0)
            {
                MessageBox.Show(this, "Файл пустой.", "Файл",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var headLen = Math.Min(4096, bytes.Length);
            if (!DocumentAttachHelper.SniffMatchesMime(bytes.AsSpan(0, headLen), mime))
            {
                MessageBox.Show(this,
                    "Содержимое не совпадает с типом файла (ожидается корректный Office/LibreOffice/PDF).",
                    "Файл", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (bytes.Length > _media.MaxDocumentBytes)
            {
                var limKb = (_media.MaxDocumentBytes + 1023) / 1024;
                MessageBox.Show(this,
                    $"В данной версии размер передаваемых файлов ограничен {limKb} КБ.",
                    "Размер", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _media.ValidateDocumentMime(mime);
            await _p2pSession!.SendFileAsync(path, bytes, mime).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send document failed in chat {ChatId}", _chat.Id);
            ShowSendError(ex.Message);
        }
        finally
        {
            await ReloadAsync().ConfigureAwait(true);
        }
    }

    private void OnAttachVoice()
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;
        EnsureSession(user);
        if (_p2pSession == null)
            return;

        if (_voiceWaveIn != null)
        {
            _voiceDiscardNextStop = false;
            try
            {
                _voiceWaveIn.StopRecording();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stop voice recording");
                CleanupVoiceRecordingHardware();
                _attachVoice.BackColor = SystemColors.Control;
            }

            return;
        }

        try
        {
            _voiceDiscardNextStop = false;
            _voiceWaveMs = new MemoryStream();
            var wf = new WaveFormat(16000, 16, 1);
            _voiceWaveWriter = new WaveFileWriter(_voiceWaveMs, wf);
            _voiceWaveIn = new WaveInEvent
            {
                WaveFormat = wf,
                BufferMilliseconds = 100
            };
            _voiceWaveIn.DataAvailable += VoiceWaveInOnDataAvailable;
            _voiceWaveIn.RecordingStopped += VoiceWaveInOnRecordingStopped;
            _voiceRecordStartUtc = DateTime.UtcNow;
            _voiceWaveIn.StartRecording();
            _attachVoice.BackColor = Color.MistyRose;
            _attachVoice.Text = "■";
            _voiceRecordTimer?.Dispose();
            _voiceRecordTimer = new Timer { Interval = 400 };
            _voiceRecordTimer.Tick += (_, _) =>
            {
                if (_voiceWaveIn == null)
                    return;
                if ((DateTime.UtcNow - _voiceRecordStartUtc).TotalSeconds >= MaxVoiceRecordSeconds)
                {
                    _voiceDiscardNextStop = false;
                    try
                    {
                        _voiceWaveIn.StopRecording();
                    }
                    catch
                    {
                        // ignore
                    }
                }
            };
            _voiceRecordTimer.Start();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Voice record start failed");
            CleanupVoiceRecordingHardware();
            ResetVoiceButton();
            MessageBox.Show(this, ex.Message, "Микрофон", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void VoiceWaveInOnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded <= 0)
            return;
        lock (_voiceCapLock)
        {
            try
            {
                _voiceWaveWriter?.Write(e.Buffer, 0, e.BytesRecorded);
            }
            catch
            {
                // disposed
            }
        }
    }

    private void VoiceWaveInOnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        byte[] wav;
        lock (_voiceCapLock)
        {
            try
            {
                _voiceWaveWriter?.Dispose();
            }
            catch
            {
                // ignore
            }

            _voiceWaveWriter = null;
            try
            {
                wav = _voiceWaveMs?.ToArray() ?? [];
            }
            catch
            {
                wav = [];
            }

            try
            {
                _voiceWaveMs?.Dispose();
            }
            catch
            {
                // ignore
            }

            _voiceWaveMs = null;
        }

        try
        {
            _voiceWaveIn?.Dispose();
        }
        catch
        {
            // ignore
        }

        _voiceWaveIn = null;

        var discard = _voiceDiscardNextStop;
        _voiceDiscardNextStop = false;

        if (!IsHandleCreated)
            return;

        BeginInvoke(new Action(async () =>
        {
            ResetVoiceButton();
            try
            {
                _voiceRecordTimer?.Stop();
            }
            catch
            {
                // ignore
            }

            try
            {
                _voiceRecordTimer?.Dispose();
            }
            catch
            {
                // ignore
            }

            _voiceRecordTimer = null;

            if (e.Exception != null)
            {
                _logger.LogWarning(e.Exception, "Voice recording stopped with error");
                MessageBox.Show(this, e.Exception.Message, "Запись", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (discard || wav.Length < 200)
                return;

            UseWaitCursor = true;
            try
            {
                var bitrate = MediaEconomy.SpeechBitrate(_routing);
                var (ok, ogg, err) = await VoiceRecordHelper
                    .EncodeWavPcmToOggOpusAsync(wav, bitrate)
                    .ConfigureAwait(true);
                if (!ok || ogg == null)
                {
                    MessageBox.Show(this, err ?? "Кодирование в Ogg не удалось.", "Голосовое",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _media.ValidateDocumentMime(VoiceRecordHelper.VoiceMessageMime);
                _media.ValidateDocumentSize(ogg.Length);
                await _p2pSession!
                    .SendFileAsync(VoiceRecordHelper.VoiceFileName, ogg, VoiceRecordHelper.VoiceMessageMime)
                    .ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Send voice failed");
                ShowSendError(ex.Message);
            }
            finally
            {
                UseWaitCursor = false;
                await ReloadAsync().ConfigureAwait(true);
            }
        }));
    }

    private void CleanupVoiceRecordingHardware()
    {
        try
        {
            _voiceRecordTimer?.Stop();
        }
        catch
        {
            // ignore
        }

        try
        {
            _voiceRecordTimer?.Dispose();
        }
        catch
        {
            // ignore
        }

        _voiceRecordTimer = null;
        lock (_voiceCapLock)
        {
            try
            {
                _voiceWaveWriter?.Dispose();
            }
            catch
            {
                // ignore
            }

            _voiceWaveWriter = null;
            try
            {
                _voiceWaveMs?.Dispose();
            }
            catch
            {
                // ignore
            }

            _voiceWaveMs = null;
        }

        try
        {
            _voiceWaveIn?.Dispose();
        }
        catch
        {
            // ignore
        }

        _voiceWaveIn = null;
    }

    private void ResetVoiceButton()
    {
        _attachVoice.BackColor = SystemColors.Control;
        _attachVoice.Text = "🎤";
    }

    private static Task<byte[]> ReadAllBytesAsync(string path) =>
        Task.Run(() => File.ReadAllBytes(path));
}
