using Iskra.Maui.Localization;

namespace Iskra.Maui;

/// <summary>Полноэкранный просмотр картинки из файла (байты грузятся только по клику).</summary>
public sealed class ImagePreviewPage : ContentPage
{
    public ImagePreviewPage(string filePath)
    {
        Title = Loc.T("image.title");
        BackgroundColor = Colors.Black;
        ToolbarItems.Add(new ToolbarItem
        {
            Text = Loc.T("close"),
            Command = new Command(async () => await CloseAsync())
        });
        Content = new Image
        {
            Source = ImageSource.FromFile(filePath),
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            BackgroundColor = Colors.Black
        };
    }

    private async Task CloseAsync()
    {
        if (Navigation.ModalStack.Count > 0)
            await Navigation.PopModalAsync().ConfigureAwait(true);
        else if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync().ConfigureAwait(true);
    }
}
