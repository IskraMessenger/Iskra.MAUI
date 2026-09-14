using Iskra.Maui.Controls;
using Iskra.Maui.Localization;
using Iskra.Maui.Services;

namespace Iskra.Maui;

/// <summary>
/// Превью видео: крупная кнопка ▶ открывает системный плеер (в MAUI нет встроенного),
/// справа внизу — кнопка «Сохранить» с системным диалогом сохранения.
/// </summary>
public sealed class VideoPreviewPage : ContentPage
{
    private readonly string _filePath;
    private readonly string _displayName;

    public VideoPreviewPage(string filePath, string? displayName = null)
    {
        _filePath = filePath;
        _displayName = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileName(filePath) : displayName;
        Title = Loc.T("chat.video");
        BackgroundColor = Colors.Black;
        ToolbarItems.Add(new ToolbarItem
        {
            Text = Loc.T("close"),
            Command = new Command(async () => await CloseAsync())
        });

        var playButton = new Border
        {
            WidthRequest = 76,
            HeightRequest = 76,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 38 },
            BackgroundColor = Color.FromArgb("#66000000"),
            Padding = 0,
            HorizontalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = "▶",
                FontSize = 30,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        };
        var playTap = new TapGestureRecognizer();
        playTap.Tapped += async (_, _) => await OpenExternalAsync().ConfigureAwait(true);
        playButton.GestureRecognizers.Add(playTap);

        Content = new Grid
        {
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 12,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        playButton,
                        new Label
                        {
                            Text = _displayName,
                            FontSize = 13,
                            TextColor = Colors.White,
                            HorizontalTextAlignment = TextAlignment.Center,
                            LineBreakMode = LineBreakMode.MiddleTruncation,
                            MaximumWidthRequest = 320
                        }
                    }
                },
                new SaveMediaFab(() => SaveAsync())
            }
        };
    }

    private async Task OpenExternalAsync()
    {
        try
        {
            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                Title = Loc.T("chat.video"),
                File = new ReadOnlyFile(_filePath)
            }).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await DisplayAlert(Loc.T("chat.video"), ex.Message, Loc.T("ok")).ConfigureAwait(true);
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            await MediaFileSaver.SaveAsync(_filePath, _displayName).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await DisplayAlert(Loc.T("save"), ex.Message, Loc.T("ok")).ConfigureAwait(true);
        }
    }

    private async Task CloseAsync()
    {
        if (Navigation.ModalStack.Count > 0)
            await Navigation.PopModalAsync().ConfigureAwait(true);
        else if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync().ConfigureAwait(true);
    }
}
