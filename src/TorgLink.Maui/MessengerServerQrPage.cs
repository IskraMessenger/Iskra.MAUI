using TorgLink.Maui.Localization;
using ShortP2P.Client.Qr;

namespace TorgLink.Maui;

public sealed class MessengerServerQrPage : ContentPage
{
    private readonly byte[] _qrPng;
    private readonly string _caption;
    private readonly Label _captionLabel = new() { FontSize = 12, TextColor = Colors.Gray };
    private readonly Button _shareButton = new() { HorizontalOptions = LayoutOptions.Center };
    private readonly Button _closeButton = new() { HorizontalOptions = LayoutOptions.Center };

    public MessengerServerQrPage(MessengerServerQrPayload payload, byte[] qrPng)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(qrPng);
        _qrPng = qrPng;
        _caption = $"{payload.H}:{payload.P}";

        var image = new Image
        {
            HeightRequest = 280,
            WidthRequest = 280,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center,
            BackgroundColor = Color.FromRgb(245, 245, 245),
            Source = ImageSource.FromStream(() => new MemoryStream(_qrPng))
        };

        _shareButton.Clicked += OnShareClicked;
        _closeButton.Clicked += async (_, _) => await Navigation.PopAsync().ConfigureAwait(true);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    _captionLabel,
                    image,
                    _shareButton,
                    _closeButton
                }
            }
        };
        ApplyLocalizedUi();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedUi();
    }

    private void ApplyLocalizedUi()
    {
        Title = Loc.T("servers.share_title");
        _captionLabel.Text = Loc.Tf("servers.qr_caption", _caption);
        _shareButton.Text = Loc.T("share");
        _closeButton.Text = Loc.T("close");
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        try
        {
            var filename = $"shortp2p-server-qr-{DateTime.UtcNow:yyyyMMddHHmmss}.png";
            var path = Path.Combine(FileSystem.CacheDirectory, filename);
            await File.WriteAllBytesAsync(path, _qrPng).ConfigureAwait(true);
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = Loc.T("servers.share_title"),
                File = new ShareFile(path)
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await DisplayAlert(Loc.T("qr.title"), Loc.Tf("qr.share_fail", ex.Message), Loc.T("ok"))
                .ConfigureAwait(true);
        }
    }
}
