using Iskra.Maui.Localization;
using Iskra.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Client;
using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;

namespace Iskra.Maui;

public partial class ChatDetailPage
{
    private void QueueBinarySend(Func<ChatP2PSession, CancellationToken, Task> work)
    {
        var session = _p2pSession;
        if (session == null)
            return;
        var mode = MediaEconomy.Mode(_p2p);
        _ = RunBinarySendAsync(session, mode, work);
    }

    private async Task RunBinarySendAsync(
        ChatP2PSession session,
        TrafficQualityMode mode,
        Func<ChatP2PSession, CancellationToken, Task> work)
    {
        try
        {
            await _p2p.BinarySends.RunAsync(mode, ct => work(session, ct)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (OutboundMessageQueuedException ex)
        {
            _logger.LogInformation(ex, "Binary queued until peer is on LAN");
            ReportBinarySendIssue(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Binary send failed");
            ReportBinarySendIssue(ex.Message);
        }
    }

    private void ReportBinarySendIssue(string message) =>
        MainThread.BeginInvokeOnMainThread(() => ShowDeliveryIssue(message));

    private Task UiAlertAsync(string title, string body) =>
        MainThread.InvokeOnMainThreadAsync(() => DisplayAlert(title, body, Loc.T("ok")));

    private static async Task<IReadOnlyList<FileResult>> PickFilesAsync(PickOptions options)
    {
        try
        {
            var many = await FilePicker.Default.PickMultipleAsync(options).ConfigureAwait(true);
            if (many != null)
                return many.Where(p => p != null).Select(p => p!).ToList();
        }
        catch (Exception)
        {
            // Some platforms only support a single pick.
        }

        var one = await FilePicker.Default.PickAsync(options).ConfigureAwait(true);
        return one == null ? [] : [one];
    }

    private async void OnAttachImageClicked(object? sender, EventArgs e)
    {
        if (_p2pSession == null)
            return;
        ClearDeliveryIssue();
        await SyncTrafficQualityAsync().ConfigureAwait(true);

        IReadOnlyList<FileResult> picks;
        try
        {
            picks = await PickFilesAsync(new PickOptions
            {
                PickerTitle = Loc.T("chat.image_picker"),
                FileTypes = FilePickerFileType.Images
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pick image failed");
            ShowDeliveryIssue(ex.Message);
            return;
        }

        foreach (var pick in picks)
            QueueBinarySend((session, ct) => SendPickedImageAsync(session, pick, ct));
    }

    private async Task SendPickedImageAsync(ChatP2PSession session, FileResult pick, CancellationToken ct)
    {
        if (!ImageAttachHelper.TryGetMimeFromExtension(pick.FileName, out var mime))
        {
            await UiAlertAsync(Loc.T("chat.file"), Loc.T("chat.only_images")).ConfigureAwait(false);
            return;
        }

        var bytes = await ReadPickBytesAsync(pick, ct).ConfigureAwait(false);
        AppLog.BinaryLoaded("image", pick.FileName, bytes.Length);
        if (bytes.Length < 12)
        {
            await UiAlertAsync(Loc.T("chat.file"), Loc.T("chat.file_too_small")).ConfigureAwait(false);
            return;
        }

        if (!ImageAttachHelper.SniffMatchesMime(bytes.AsSpan(0, Math.Min(12, bytes.Length)), mime))
        {
            await UiAlertAsync(Loc.T("chat.file"), Loc.T("chat.file_mismatch")).ConfigureAwait(false);
            return;
        }

        var fitted = FitOutgoingImage(bytes, mime);
        if (fitted.Bytes == null)
        {
            await UiAlertAsync(Loc.T("chat.compress"), fitted.Error ?? Loc.T("chat.compress_fail"))
                .ConfigureAwait(false);
            return;
        }

        _media.ValidateMime(fitted.Mime);
        await PrepareBinarySendAsync().ConfigureAwait(false);
        await session.SendImageAsync(fitted.Bytes, fitted.Mime, ct).ConfigureAwait(false);
        MainThread.BeginInvokeOnMainThread(ClearDeliveryIssue);
    }

    private async void OnAttachDocumentClicked(object? sender, EventArgs e)
    {
        if (_p2pSession == null)
            return;
        ClearDeliveryIssue();
        await SyncTrafficQualityAsync().ConfigureAwait(true);

        IReadOnlyList<FileResult> picks;
        try
        {
            picks = await PickFilesAsync(new PickOptions
            {
                PickerTitle = Loc.T("chat.doc_picker"),
                FileTypes = OfficeDocFileTypes
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pick document failed");
            ShowDeliveryIssue(ex.Message);
            return;
        }

        foreach (var pick in picks)
            QueueBinarySend((session, ct) => SendPickedDocumentAsync(session, pick, ct));
    }

    private async Task SendPickedDocumentAsync(ChatP2PSession session, FileResult pick, CancellationToken ct)
    {
        if (!TryGetDocumentOrVideoMime(pick.FileName, out var mime))
        {
            await UiAlertAsync(Loc.T("chat.file"), Loc.T("chat.only_docs_video")).ConfigureAwait(false);
            return;
        }

        var bytes = await ReadPickBytesAsync(pick, ct).ConfigureAwait(false);
        AppLog.BinaryLoaded("document", pick.FileName, bytes.Length);
        if (bytes.Length == 0)
        {
            await UiAlertAsync(Loc.T("chat.file"), Loc.T("chat.file_empty")).ConfigureAwait(false);
            return;
        }

        var sendName = pick.FileName;
        var isVideo = Video144pTranscoder.IsVideoMime(mime);
        if (!isVideo)
        {
            var headLen = Math.Min(4096, bytes.Length);
            if (!DocumentAttachHelper.SniffMatchesMime(bytes.AsSpan(0, headLen), mime))
            {
                await UiAlertAsync(Loc.T("chat.file"), Loc.T("chat.file_type_mismatch")).ConfigureAwait(false);
                return;
            }
        }

        var prepared = await PrepareOutgoingFileAsync(bytes, sendName, mime, isVideo, ct).ConfigureAwait(false);
        if (prepared == null)
            return;

        _media.ValidateDocumentMime(prepared.Value.Mime);
        await PrepareBinarySendAsync().ConfigureAwait(false);
        await session.SendFileAsync(prepared.Value.FileName, prepared.Value.Bytes, prepared.Value.Mime, ct)
            .ConfigureAwait(false);
        MainThread.BeginInvokeOnMainThread(ClearDeliveryIssue);
    }

    private async void OnAttachCameraClicked(object? sender, EventArgs e)
    {
        if (_p2pSession == null)
            return;
        ClearDeliveryIssue();

        var photoLabel = Loc.T("preview.photo");
        var videoLabel = Loc.T("chat.video");
        var choice = await DisplayActionSheet(Loc.T("chat.camera"), Loc.T("cancel"), null, photoLabel, videoLabel)
            .ConfigureAwait(true);
        if (string.IsNullOrEmpty(choice) || choice == Loc.T("cancel"))
            return;

        if (choice == photoLabel)
            await CaptureAndQueueCameraPhotoAsync().ConfigureAwait(true);
        else if (choice == videoLabel)
            await CaptureAndQueueCameraVideoAsync().ConfigureAwait(true);
    }

    private async Task CaptureAndQueueCameraPhotoAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_unsupported"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

            var cam = await Permissions.RequestAsync<Permissions.Camera>().ConfigureAwait(true);
            if (cam != PermissionStatus.Granted)
            {
                await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_perm"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

#if ANDROID
            await EnsureLegacyStorageWriteAsync().ConfigureAwait(true);
#endif

            var photo = await MediaPicker.Default.CapturePhotoAsync().ConfigureAwait(true);
            if (photo == null)
                return;

            await SyncTrafficQualityAsync().ConfigureAwait(true);
            QueueBinarySend((session, ct) => SendCameraPhotoAsync(session, photo, ct));
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_unsupported"), Loc.T("ok"))
                .ConfigureAwait(true);
        }
        catch (PermissionException)
        {
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_perm"), Loc.T("ok"))
                .ConfigureAwait(true);
        }
        catch (FileNotFoundException ex) when (IsAppxManifestMissing(ex))
        {
            _logger.LogWarning(ex, "Camera photo failed: AppxManifest missing");
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_windows_manifest"), Loc.T("ok"))
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Capture camera photo failed");
            ShowDeliveryIssue(ex.Message);
        }
    }

    private async Task SendCameraPhotoAsync(ChatP2PSession session, FileResult photo, CancellationToken ct)
    {
        var bytes = await ReadPickBytesAsync(photo, ct).ConfigureAwait(false);
        var fileName = string.IsNullOrWhiteSpace(photo.FileName)
            ? $"camera-{DateTime.UtcNow:yyyyMMdd-HHmmss}.jpg"
            : photo.FileName;
        AppLog.BinaryLoaded("camera-photo", fileName, bytes.Length);
        if (bytes.Length < 12)
        {
            await UiAlertAsync(Loc.T("chat.camera"), Loc.T("chat.camera_photo_fail")).ConfigureAwait(false);
            return;
        }

        if (!ImageAttachHelper.TryGetMimeFromExtension(fileName, out var mime))
            mime = "image/jpeg";

        var fitted = FitOutgoingImage(bytes, mime);
        if (fitted.Bytes == null)
        {
            await UiAlertAsync(Loc.T("chat.compress"), fitted.Error ?? Loc.T("chat.compress_fail"))
                .ConfigureAwait(false);
            return;
        }

        _media.ValidateMime(fitted.Mime);
        await PrepareBinarySendAsync().ConfigureAwait(false);
        await session.SendImageAsync(fitted.Bytes, fitted.Mime, ct).ConfigureAwait(false);
        MainThread.BeginInvokeOnMainThread(ClearDeliveryIssue);
    }

    private async Task CaptureAndQueueCameraVideoAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_unsupported"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

            var cam = await Permissions.RequestAsync<Permissions.Camera>().ConfigureAwait(true);
            if (cam != PermissionStatus.Granted)
            {
                await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_perm"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

            var mic = await Permissions.RequestAsync<Permissions.Microphone>().ConfigureAwait(true);
            if (mic != PermissionStatus.Granted)
            {
                await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_mic_perm"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

#if ANDROID
            await EnsureLegacyStorageWriteAsync().ConfigureAwait(true);
#endif

            var video = await MediaPicker.Default.CaptureVideoAsync().ConfigureAwait(true);
            if (video == null)
                return;

            await SyncTrafficQualityAsync().ConfigureAwait(true);
            QueueBinarySend((session, ct) => SendCameraVideoAsync(session, video, ct));
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_unsupported"), Loc.T("ok"))
                .ConfigureAwait(true);
        }
        catch (PermissionException)
        {
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_perm"), Loc.T("ok"))
                .ConfigureAwait(true);
        }
        catch (FileNotFoundException ex) when (IsAppxManifestMissing(ex))
        {
            _logger.LogWarning(ex, "Camera video failed: AppxManifest missing");
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_windows_manifest"), Loc.T("ok"))
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Capture camera video failed");
            ShowDeliveryIssue(ex.Message);
        }
    }

    private async Task SendCameraVideoAsync(ChatP2PSession session, FileResult video, CancellationToken ct)
    {
        var bytes = await ReadPickBytesAsync(video, ct).ConfigureAwait(false);
        var fileName = string.IsNullOrWhiteSpace(video.FileName)
            ? $"camera-{DateTime.UtcNow:yyyyMMdd-HHmmss}.mp4"
            : video.FileName;
        AppLog.BinaryLoaded("camera-video", fileName, bytes.Length);
        if (bytes.Length < 32)
        {
            await UiAlertAsync(Loc.T("chat.camera"), Loc.T("chat.camera_fail")).ConfigureAwait(false);
            return;
        }

        if (!TryGetDocumentOrVideoMime(fileName, out var mime))
            mime = "video/mp4";

        var prepared = await PrepareOutgoingFileAsync(bytes, fileName, mime, isVideo: true, ct).ConfigureAwait(false);
        if (prepared == null)
            return;

        _media.ValidateDocumentMime(prepared.Value.Mime);
        await PrepareBinarySendAsync().ConfigureAwait(false);
        await session.SendFileAsync(prepared.Value.FileName, prepared.Value.Bytes, prepared.Value.Mime, ct)
            .ConfigureAwait(false);
        MainThread.BeginInvokeOnMainThread(ClearDeliveryIssue);
    }

    private async Task FinishAndSendVoiceAsync(
        ChatP2PSession session, VoiceRecordingSession voice, CancellationToken ct)
    {
        try
        {
            var recorded = await voice.TakeResultAsync(ct).ConfigureAwait(false);
            AppLog.BinaryLoaded("voice", recorded.FileName, recorded.Bytes.Length);
            _media.ValidateDocumentMime(recorded.MimeType);
            _media.ValidateDocumentSize(recorded.Bytes.Length);
            await PrepareBinarySendAsync().ConfigureAwait(false);
            await session.SendFileAsync(recorded.FileName, recorded.Bytes, recorded.MimeType, ct)
                .ConfigureAwait(false);
            MainThread.BeginInvokeOnMainThread(ClearDeliveryIssue);
        }
        finally
        {
            try
            {
                await voice.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static async Task<byte[]> ReadPickBytesAsync(FileResult pick, CancellationToken ct)
    {
        await using var stream = await pick.OpenReadAsync().ConfigureAwait(false);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        return ms.ToArray();
    }

    private (byte[]? Bytes, string Mime, string? Error) FitOutgoingImage(byte[] bytes, string mime)
    {
        var limit = MediaEconomy.ImageLimit(_media, _p2p);
        if (bytes.Length <= limit)
            return (bytes, mime, null);

        if (!ImageAttachmentCompressor.TryCompressToMaxBytes(bytes, limit, out var compressed, out var err))
            return (null, mime, err ?? Loc.T("chat.compress_fail"));

        return (compressed, ImageAttachmentCompressor.SuggestMimeAfterCompression(), null);
    }

    private async Task<(byte[] Bytes, string Mime, string FileName)?> PrepareOutgoingFileAsync(
        byte[] bytes, string fileName, string mime, bool isVideo, CancellationToken ct)
    {
        if (isVideo && MediaEconomy.UsesReducedMedia(_p2p))
        {
            var ext = Path.GetExtension(fileName);
            var temp = Path.Combine(FileSystem.CacheDirectory,
                $"iskra_bin_{DateTime.UtcNow.Ticks}{(string.IsNullOrEmpty(ext) ? ".mp4" : ext)}");
            await File.WriteAllBytesAsync(temp, bytes, ct).ConfigureAwait(false);
            try
            {
                var prepared = await Video144pTranscoder
                    .PrepareAsync(temp, fileName, mime, MediaEconomy.Mode(_p2p))
                    .ConfigureAwait(false);
                if (!prepared.Ok || prepared.Bytes == null)
                {
                    await UiAlertAsync(Loc.T("chat.video"),
                            prepared.Error ?? Loc.Tf("chat.video_transcode_fail",
                                MediaEconomy.VideoResolutionLabel(_p2p)))
                        .ConfigureAwait(false);
                    return null;
                }

                bytes = prepared.Bytes;
                mime = prepared.Mime;
                fileName = prepared.FileName;
                AppLog.BinaryLoaded("video-economy", fileName, bytes.Length);
            }
            finally
            {
                try
                {
                    File.Delete(temp);
                }
                catch
                {
                    // ignore
                }
            }
        }
        else if (bytes.Length > _media.MaxDocumentBytes)
        {
            var limMb = (_media.MaxDocumentBytes + (1024 * 1024 - 1)) / (1024 * 1024);
            await UiAlertAsync(Loc.T("chat.size"), Loc.Tf("chat.size_over", limMb)).ConfigureAwait(false);
            return null;
        }

        if (bytes.Length > _media.MaxDocumentBytes)
        {
            await UiAlertAsync(Loc.T("chat.size"), Loc.T("chat.size_still")).ConfigureAwait(false);
            return null;
        }

        return (bytes, mime, fileName);
    }

    private async Task PrepareBinarySendAsync()
    {
        await MessengerServersBootstrap.EnsureRunningAsync(_p2p, _logger).ConfigureAwait(false);
    }
}
