namespace TorgLink.Maui.Services;

internal sealed class VoiceRecordingResult
{
    public required byte[] Bytes { get; init; }
    public required string FileName { get; init; }
    public required string MimeType { get; init; }
}

internal sealed class VoiceRecordingSession : IAsyncDisposable
{
    public const string OggMime = "audio/ogg";
    public const string OggFileName = "voice.ogg";

#if ANDROID
    private global::Android.Media.MediaRecorder? _androidRecorder;
#elif WINDOWS
    private global::Windows.Media.Capture.MediaCapture? _windowsCapture;
    private global::Windows.Media.Capture.LowLagMediaRecording? _windowsRecording;
#elif IOS || MACCATALYST
    private AVFoundation.AVAudioRecorder? _iosRecorder;
#endif
    private string? _tempPath;
    private bool _recording;
    private int _speechBitrateBps = MediaEconomy.DefaultSpeechBitrateBps;

    public bool IsRecording => _recording;

    public async Task StartAsync(int speechBitrateBps = MediaEconomy.DefaultSpeechBitrateBps)
    {
        if (_recording)
            return;
        _speechBitrateBps = Math.Clamp(speechBitrateBps, MediaEconomy.MinVoiceBitrateBps, 64_000);

#if ANDROID
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
            throw new InvalidOperationException("Голосовые сообщения требуют Android 10+ (API 29).");

        _tempPath = Path.Combine(FileSystem.CacheDirectory, $"voice_{DateTime.UtcNow.Ticks}.ogg");
        var recorder = new global::Android.Media.MediaRecorder();
        _androidRecorder = recorder;
        recorder.SetAudioSource(global::Android.Media.AudioSource.Mic);
        recorder.SetOutputFormat(global::Android.Media.OutputFormat.Ogg);
        recorder.SetAudioEncoder(global::Android.Media.AudioEncoder.Opus);
        recorder.SetAudioEncodingBitRate(_speechBitrateBps);
        recorder.SetAudioSamplingRate(48_000);
        recorder.SetOutputFile(_tempPath);
        recorder.Prepare();
        recorder.Start();
        _recording = true;
        await Task.CompletedTask.ConfigureAwait(false);
#elif WINDOWS
        await MainThread.InvokeOnMainThreadAsync(StartWindowsAsync).ConfigureAwait(false);
#elif IOS || MACCATALYST
        StartIos();
        await Task.CompletedTask.ConfigureAwait(false);
#else
        throw new NotSupportedException("Запись голоса на этой платформе не поддерживается.");
#endif
    }

    public Task<VoiceRecordingResult> StopAsync() => StopAndTakeResultAsync();

    /// <summary>Stops the microphone. Leaves the temp file for <see cref="TakeResultAsync"/>.</summary>
    public async Task StopCaptureAsync()
    {
        if (!_recording)
            throw new InvalidOperationException("Recording is not active.");

#if ANDROID
        try
        {
            _androidRecorder?.Stop();
        }
        finally
        {
            ReleaseAndroid();
            _recording = false;
        }

        await Task.CompletedTask.ConfigureAwait(false);
#elif WINDOWS
        await MainThread.InvokeOnMainThreadAsync(StopWindowsAsync).ConfigureAwait(false);
        _recording = false;
#elif IOS || MACCATALYST
        StopIos();
        _recording = false;
        await Task.CompletedTask.ConfigureAwait(false);
#else
        throw new NotSupportedException("Запись голоса на этой платформе не поддерживается.");
#endif
    }

    /// <summary>Reads / encodes the captured file. Safe to run off the UI thread.</summary>
    public Task<VoiceRecordingResult> TakeResultAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if ANDROID
        return ReadOggTempAsync(cancellationToken);
#else
        return Task.FromResult(EncodeWavTempToOgg(_speechBitrateBps));
#endif
    }

    private async Task<VoiceRecordingResult> StopAndTakeResultAsync()
    {
        await StopCaptureAsync().ConfigureAwait(false);
        return await TakeResultAsync().ConfigureAwait(false);
    }

    public async Task DiscardAsync()
    {
        try
        {
#if ANDROID
            try
            {
                _androidRecorder?.Stop();
            }
            catch
            {
                // ignore invalid state
            }

            ReleaseAndroid();
#elif WINDOWS
            await MainThread.InvokeOnMainThreadAsync(StopWindowsAsync).ConfigureAwait(false);
#elif IOS || MACCATALYST
            StopIos();
#endif
        }
        catch
        {
            // ignore
        }
        finally
        {
            _recording = false;
            DeleteTemp();
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => new(DiscardAsync());

#if WINDOWS
    private async Task StartWindowsAsync()
    {
        _tempPath = Path.Combine(FileSystem.CacheDirectory, $"voice_{DateTime.UtcNow.Ticks}.wav");
        var folder = await global::Windows.Storage.StorageFolder.GetFolderFromPathAsync(FileSystem.CacheDirectory);
        var file = await folder.CreateFileAsync(Path.GetFileName(_tempPath),
            global::Windows.Storage.CreationCollisionOption.ReplaceExisting);
        _tempPath = file.Path;

        var capture = new global::Windows.Media.Capture.MediaCapture();
        await capture.InitializeAsync(new global::Windows.Media.Capture.MediaCaptureInitializationSettings
        {
            StreamingCaptureMode = global::Windows.Media.Capture.StreamingCaptureMode.Audio
        });
        var profile = global::Windows.Media.MediaProperties.MediaEncodingProfile.CreateWav(
            global::Windows.Media.MediaProperties.AudioEncodingQuality.Medium);
        profile.Audio = global::Windows.Media.MediaProperties.AudioEncodingProperties.CreatePcm(48_000, 1, 16);
        _windowsCapture = capture;
        _windowsRecording = await capture.PrepareLowLagRecordToStorageFileAsync(profile, file);
        await _windowsRecording.StartAsync();
        _recording = true;
    }

    private async Task StopWindowsAsync()
    {
        try
        {
            if (_windowsRecording != null)
                await _windowsRecording.StopAsync();
        }
        finally
        {
            _windowsRecording = null;
            _windowsCapture?.Dispose();
            _windowsCapture = null;
        }
    }
#endif

#if IOS || MACCATALYST
    private void StartIos()
    {
        var session = AVFoundation.AVAudioSession.SharedInstance();
        session.SetCategory(AVFoundation.AVAudioSessionCategory.PlayAndRecord,
            AVFoundation.AVAudioSessionCategoryOptions.DefaultToSpeaker, out var catErr);
        if (catErr != null)
            throw new InvalidOperationException(catErr.LocalizedDescription);
        session.SetActive(true, out var actErr);
        if (actErr != null)
            throw new InvalidOperationException(actErr.LocalizedDescription);

        _tempPath = Path.Combine(FileSystem.CacheDirectory, $"voice_{DateTime.UtcNow.Ticks}.wav");
        var url = Foundation.NSUrl.FromFilename(_tempPath);
        var settings = new AVFoundation.AudioSettings
        {
            Format = AudioToolbox.AudioFormatType.LinearPCM,
            SampleRate = 48_000,
            NumberChannels = 1,
            AudioQuality = AVFoundation.AVAudioQuality.Medium,
            LinearPcmBitDepth = 16,
            LinearPcmFloat = false,
            LinearPcmBigEndian = false
        };
        var recorder = AVFoundation.AVAudioRecorder.Create(url, settings, out var error);
        if (recorder == null)
            throw new InvalidOperationException(error?.LocalizedDescription ?? "AVAudioRecorder failed.");
        if (!recorder.PrepareToRecord() || !recorder.Record())
            throw new InvalidOperationException("Не удалось начать запись.");
        _iosRecorder = recorder;
        _recording = true;
    }

    private void StopIos()
    {
        try
        {
            _iosRecorder?.Stop();
            _iosRecorder?.Dispose();
        }
        finally
        {
            _iosRecorder = null;
        }
    }
#endif

#if ANDROID
    private void ReleaseAndroid()
    {
        try
        {
            _androidRecorder?.Release();
            _androidRecorder?.Dispose();
        }
        catch
        {
            // ignore
        }

        _androidRecorder = null;
    }
#endif

    private async Task<VoiceRecordingResult> ReadOggTempAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_tempPath) || !File.Exists(_tempPath))
            throw new InvalidOperationException("Не удалось получить записанный голосовой файл.");
        var bytes = await File.ReadAllBytesAsync(_tempPath, cancellationToken).ConfigureAwait(false);
        DeleteTemp();
        if (bytes.Length == 0)
            throw new InvalidOperationException("Голосовая запись пустая.");
        return new VoiceRecordingResult { Bytes = bytes, FileName = OggFileName, MimeType = OggMime };
    }

    private VoiceRecordingResult EncodeWavTempToOgg(int bitrateBps)
    {
        if (string.IsNullOrEmpty(_tempPath) || !File.Exists(_tempPath))
            throw new InvalidOperationException("Не удалось получить записанный голосовой файл.");
        try
        {
            var bytes = OggOpusEncoder.EncodeWavPcm(_tempPath, bitrateBps);
            if (bytes.Length == 0)
                throw new InvalidOperationException("Голосовая запись пустая.");
            return new VoiceRecordingResult { Bytes = bytes, FileName = OggFileName, MimeType = OggMime };
        }
        finally
        {
            DeleteTemp();
        }
    }

    private void DeleteTemp()
    {
        if (string.IsNullOrEmpty(_tempPath))
            return;
        try
        {
            File.Delete(_tempPath);
        }
        catch
        {
            // ignore
        }

        _tempPath = null;
    }
}
