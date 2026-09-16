using Iskra.Maui.Localization;

namespace Iskra.Maui;

/// <summary>
/// Просмотр контакта: ник и Network ID.
/// На будущее: «О себе» и аватар (не более <see cref="MaxAvatarBytes"/> и <see cref="MaxAvatarDimension"/>×<see cref="MaxAvatarDimension"/>).
/// </summary>
public sealed class ContactDetailsPage : ContentPage
{
    public const int MaxAvatarBytes = 20 * 1024;
    public const int MaxAvatarDimension = 512;

    public ContactDetailsPage(string nickname, string networkIdShort)
    {
        var nick = string.IsNullOrWhiteSpace(nickname) ? networkIdShort : nickname.Trim();
        var id = networkIdShort?.Trim() ?? "";

        Title = Loc.T("contact.title");
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        SetDynamicResource(BackgroundColorProperty, "PageBackground");

        var back = new Button
        {
            Text = "\u2039",
            FontSize = 28,
            BackgroundColor = Colors.Transparent,
            Padding = 0,
            WidthRequest = 36,
            HeightRequest = 40
        };
        back.SetDynamicResource(Button.TextColorProperty, "MidnightBlue");
        back.Clicked += async (_, _) =>
        {
            if (Navigation.NavigationStack.Count > 1)
                await Navigation.PopAsync().ConfigureAwait(true);
        };

        var title = new Label
        {
            Text = Loc.T("contact.title"),
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        };
        title.SetDynamicResource(Label.TextColorProperty, "MidnightBlue");

        var header = new Grid
        {
            Padding = new Thickness(12, 8, 16, 8),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
            ColumnSpacing = 4,
            Children = { back, title }
        };
        Grid.SetColumn(title, 1);

        var avatar = new Border
        {
            WidthRequest = 96,
            HeightRequest = 96,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 48 },
            BackgroundColor = IskraTheme.AvatarColor(id),
            HorizontalOptions = LayoutOptions.Center
        };
        // Future: replace initials with Image when avatar blob ≤ MaxAvatarBytes and ≤ MaxAvatarDimension.
        var initials = new Label
        {
            Text = IskraTheme.Initials(nick),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 32,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        var avatarHost = new Grid
        {
            WidthRequest = 96,
            HeightRequest = 96,
            HorizontalOptions = LayoutOptions.Center,
            Children = { avatar, initials }
        };

        var nickLabel = FieldLabel(Loc.T("contact.nickname"));
        var nickValue = FieldValue(nick);
        var idLabel = FieldLabel(Loc.T("contact.network_id"));
        var idValue = FieldValue(id);
        var aboutLabel = FieldLabel(Loc.T("contact.about"));
        var aboutValue = FieldValue(Loc.T("contact.not_set"));
        aboutValue.TextColor = IskraTheme.Muted;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(0, 0, 0, 24),
                Spacing = 0,
                Children =
                {
                    header,
                    new VerticalStackLayout
                    {
                        Padding = new Thickness(24, 16, 24, 8),
                        Spacing = 16,
                        Children =
                        {
                            avatarHost,
                            nickLabel,
                            nickValue,
                            idLabel,
                            idValue,
                            aboutLabel,
                            aboutValue
                        }
                    }
                }
            }
        };
    }

    private static Label FieldLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0)
        };
        label.SetDynamicResource(Label.TextColorProperty, "MutedText");
        return label;
    }

    private static Label FieldValue(string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 17,
            LineBreakMode = LineBreakMode.WordWrap
        };
        label.SetDynamicResource(Label.TextColorProperty, "MidnightBlue");
        return label;
    }
}
