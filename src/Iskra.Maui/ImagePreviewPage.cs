using Iskra.Maui.Controls;
using Iskra.Maui.Localization;
using Iskra.Maui.Services;

namespace Iskra.Maui;

/// <summary>Полноэкранный просмотр картинки из файла (байты грузятся только по клику).</summary>
public sealed class ImagePreviewPage : ContentPage
{
    private readonly string _filePath;
    private readonly string _displayName;

    public ImagePreviewPage(string filePath, string? displayName = null)
    {
        _filePath = filePath;
        _displayName = string.IsNullOrWhiteSpace(displayName) ? Path.GetFileName(filePath) : displayName;
        Title = Loc.T("image.title");
        BackgroundColor = Colors.Black;
        ToolbarItems.Add(new ToolbarItem
        {
            Text = Loc.T("close"),
            Command = new Command(async () => await CloseAsync())
        });
        Content = new Grid
        {
            Children =
            {
                new Image
                {
                    Source = ImageSource.FromFile(filePath),
                    Aspect = Aspect.AspectFit,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    BackgroundColor = Colors.Black
                },
                new SaveMediaFab(() => SaveAsync())
            }
        };
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
