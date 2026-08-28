using Iskra.Maui.Localization;
using Iskra.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.Qr;
using ShortP2P.Client.Services;
using ShortP2P.Crypto;

namespace Iskra.Maui;

public partial class AddChatPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ChatRepository _chats;
    private readonly ILogger<AddChatPage> _logger;
    private readonly UserP2pRuntime _p2p;

    public AddChatPage(AuthService auth, ChatRepository chats, UserP2pRuntime p2p, ILogger<AddChatPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _chats = chats;
        _p2p = p2p;
        _logger = logger;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedUi();
    }

    private void ApplyLocalizedUi()
    {
        Title = Loc.T("addchat.title");
        CancelToolbar.Text = Loc.T("cancel");
        NickLabel.Text = Loc.T("addchat.nick");
        IdLabel.Text = Loc.T("addchat.id");
        PubKeyLabel.Text = Loc.T("addchat.pubkey");
        HostLabel.Text = Loc.T("addchat.host");
        PortLabel.Text = Loc.T("addchat.port");
        PeerNickEntry.Placeholder = Loc.T("addchat.ph_nick");
        PeerIdEntry.Placeholder = Loc.T("addchat.ph_id");
        PeerPubKeyEditor.Placeholder = Loc.T("addchat.ph_key");
        ScanCamButton.Text = Loc.T("addchat.scan_cam");
        ScanImgButton.Text = Loc.T("addchat.scan_img");
        SaveButton.Text = Loc.T("save");
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync().ConfigureAwait(true);
    }

    private async void OnScanQrClicked(object? sender, EventArgs e)
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = Loc.T("addchat.qr_picker"),
            FileTypes = FilePickerFileType.Images
        }).ConfigureAwait(true);

        if (result == null)
            return;

        await using var stream = await result.OpenReadAsync().ConfigureAwait(true);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms).ConfigureAwait(true);
        var bytes = ms.ToArray();
        AppLog.BinaryLoaded("peer-qr", result.FileName, bytes.Length);

        if (!PeerQrService.TryDecodeImage(bytes, out var payload, out var err))
        {
            _logger.LogWarning("QR decode failed from file: {Error}", err);
            await DisplayAlert(Loc.T("qr.title"), err ?? Loc.T("addchat.qr_fail"), Loc.T("ok")).ConfigureAwait(true);
            return;
        }

        ApplyQrPayload(payload);
        await TryInstallChatFromQrAsync(payload).ConfigureAwait(true);
    }

    private async void OnScanQrCameraClicked(object? sender, EventArgs e)
    {
        if (!await EnsureCameraPermissionAsync().ConfigureAwait(true))
            return;

#if ANDROID
        try
        {
            var qrText = await MainActivity.TryScanQrWithSystemScannerAsync().ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(qrText))
            {
                if (PeerQrCodec.TryDeserialize(qrText.Trim(), out var payloadFromScanner, out var errFromScanner))
                {
                    ApplyQrPayload(payloadFromScanner);
                    await TryInstallChatFromQrAsync(payloadFromScanner).ConfigureAwait(true);
                    return;
                }

                _logger.LogWarning("System QR scanner returned invalid payload: {Error}", errFromScanner);
                await DisplayAlert(Loc.T("qr.title"), errFromScanner ?? Loc.T("addchat.qr_bad"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "System QR scanner invocation failed");
        }
#endif

        FileResult? photo;
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert(Loc.T("chat.camera"), Loc.T("addchat.cam_unsupported"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

            photo = await MediaPicker.Default.CapturePhotoAsync().ConfigureAwait(true);
        }
        catch (PermissionException ex)
        {
            _logger.LogWarning(ex, "Camera permission denied");
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("addchat.cam_perm"), Loc.T("ok")).ConfigureAwait(true);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Camera capture failed");
            await DisplayAlert(Loc.T("chat.camera"), Loc.Tf("addchat.cam_open", ex.Message), Loc.T("ok"))
                .ConfigureAwait(true);
            return;
        }

        if (photo == null)
            return;

        await using var stream = await photo.OpenReadAsync().ConfigureAwait(true);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms).ConfigureAwait(true);
        var bytes = ms.ToArray();
        AppLog.BinaryLoaded("peer-qr-camera", photo.FileName, bytes.Length);

        if (!PeerQrService.TryDecodeImage(bytes, out var payload, out var err))
        {
            _logger.LogWarning("QR decode failed from camera photo: {Error}", err);
            await DisplayAlert(Loc.T("qr.title"), err ?? Loc.T("addchat.cam_photo"), Loc.T("ok")).ConfigureAwait(true);
            return;
        }

        ApplyQrPayload(payload);
        await TryInstallChatFromQrAsync(payload).ConfigureAwait(true);
    }

    private async Task<bool> EnsureCameraPermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>().ConfigureAwait(true);
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.Camera>().ConfigureAwait(true);
            if (status == PermissionStatus.Granted)
                return true;
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("addchat.cam_perm"), Loc.T("ok")).ConfigureAwait(true);
            return false;
        }
        catch (FileNotFoundException ex) when (
            ex.FileName?.Contains("AppxManifest.xml", StringComparison.OrdinalIgnoreCase) == true ||
            ex.Message.Contains("AppxManifest.xml", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "Camera permission failed: AppxManifest missing");
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_windows_manifest"), Loc.T("ok"))
                .ConfigureAwait(true);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Camera permission request failed");
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("addchat.cam_perm_req"), Loc.T("ok")).ConfigureAwait(true);
            return false;
        }
    }

    private void ApplyQrPayload(PeerQrPayload payload)
    {
        PeerNickEntry.Text = payload.N;
        PeerIdEntry.Text = payload.Id;
        PeerPubKeyEditor.Text = payload.K;
        PeerHostEntry.Text = payload.GetCommaSeparatedHosts();
        PeerPortEntry.Text = payload.P.ToString();
    }

    private async Task TryInstallChatFromQrAsync(PeerQrPayload payload)
    {
        var u = _auth.CurrentUser;
        if (u == null)
            return;

        try
        {
            var chat = await _chats
                .AddChatAsync(u.Id, payload.N, payload.Id, payload.K, payload.GetCommaSeparatedHosts(), payload.P,
                    keySource: PeerKeySource.Qr())
                .ConfigureAwait(true);

            await _p2p.TryEnsureChatSessionStartedAsync(chat.Id, SynchronizationContext.Current).ConfigureAwait(true);

            await Navigation.PopModalAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QR auto-install failed");
            await DisplayAlert(Loc.T("qr.title"), Loc.Tf("addchat.auto_fail", ex.Message), Loc.T("ok"))
                .ConfigureAwait(true);
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var u = _auth.CurrentUser;
        if (u == null)
        {
            await DisplayAlert(Loc.T("error"), Loc.T("addchat.not_logged"), Loc.T("ok")).ConfigureAwait(true);
            return;
        }

        var nick = PeerNickEntry.Text?.Trim() ?? "";
        var id = PeerIdEntry.Text?.Trim() ?? "";
        var pub = PeerPubKeyEditor.Text?.Trim() ?? "";
        var host = PeerHostEntry.Text?.Trim() ?? "";
        if (!int.TryParse(PeerPortEntry.Text, out var port))
        {
            await DisplayAlert(Loc.T("error"), Loc.T("addchat.bad_port"), Loc.T("ok")).ConfigureAwait(true);
            return;
        }

        if (nick.Length == 0 || id.Length == 0 || pub.Length == 0 || host.Length == 0)
        {
            await DisplayAlert(Loc.T("error"), Loc.T("addchat.fill_all"), Loc.T("ok")).ConfigureAwait(true);
            return;
        }

        try
        {
            _ = RsaKeySerializer.DeserializePublic(pub);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid public key when saving chat");
            await DisplayAlert(Loc.T("error"), Loc.T("addchat.bad_key"), Loc.T("ok")).ConfigureAwait(true);
            return;
        }

        var chat = await _chats.AddChatAsync(u.Id, nick, id, pub, host, port, keySource: PeerKeySource.Manual())
            .ConfigureAwait(true);
        try
        {
            await _p2p.TryEnsureChatSessionStartedAsync(chat.Id, SynchronizationContext.Current).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Start P2P session after add chat");
        }

        await Navigation.PopModalAsync().ConfigureAwait(true);
    }
}
