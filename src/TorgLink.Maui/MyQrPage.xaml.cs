using TorgLink.Maui.Localization;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client.Qr;
using ShortP2P.Crypto;

namespace TorgLink.Maui;

public partial class MyQrPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ILogger<MyQrPage> _logger;
    private byte[]? _currentQrPng;

    public MyQrPage(AuthService auth, ILogger<MyQrPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _logger = logger;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Title = Loc.T("myqr.title");
        HintLabel.Text = Loc.T("myqr.hint");
        ShareButton.Text = Loc.T("share");
        var u = _auth.CurrentUser;
        if (u == null)
        {
            _logger.LogWarning("My QR: user not logged in");
            return;
        }

        var pub = RsaKeySerializer.SerializePublic(_auth.GetCurrentPublicKey());
        _ = RenderQrAsync(u, pub);
    }

    private async Task RenderQrAsync(UserEntity u, string pub)
    {
        try
        {
            // InviteHostsBuilder → GetAllUnicastIpv6Ordered / public IP — blocking; off UI thread.
            var png = await Task.Run(() =>
            {
                var payload = PeerQrService.BuildPayload(u, pub);
                return PeerQrService.EncodeQrPng(payload);
            }).ConfigureAwait(true);
            _currentQrPng = png;
            QrImage.Source = ImageSource.FromStream(() => new MemoryStream(png));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Render my QR failed");
        }
    }

    private async void OnShareQrClicked(object? sender, EventArgs e)
    {
        if (_currentQrPng == null || _currentQrPng.Length == 0)
        {
            await DisplayAlert(Loc.T("qr.title"), Loc.T("qr.not_ready"), Loc.T("ok")).ConfigureAwait(true);
            return;
        }

        try
        {
            var filename = $"shortp2p-my-qr-{DateTime.UtcNow:yyyyMMddHHmmss}.png";
            var path = Path.Combine(FileSystem.CacheDirectory, filename);
            await File.WriteAllBytesAsync(path, _currentQrPng).ConfigureAwait(true);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = Loc.T("qr.share"),
                File = new ShareFile(path)
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Share QR failed");
            await DisplayAlert(Loc.T("qr.title"), Loc.Tf("qr.share_fail", ex.Message), Loc.T("ok"))
                .ConfigureAwait(true);
        }
    }
}
