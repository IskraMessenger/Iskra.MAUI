namespace Iskra.Maui;

public partial class AppShell : Shell
{
    public AppShell(ChatsPage chats, ContactsPage contacts, NetworkPage network, SettingsPage settings)
    {
        InitializeComponent();
        ChatsHost.Content = chats;
        ContactsHost.Content = contacts;
        NetworkHost.Content = network;
        SettingsHost.Content = settings;
        ApplyChrome();
        ThemeService.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(ApplyChrome);
    }

    private void ApplyChrome()
    {
        var p = IskraTheme.Current;
        BackgroundColor = p.PageBackground;
        FlyoutBackgroundColor = p.PageBackground;
        SetValue(TabBarBackgroundColorProperty, p.Surface);
        SetValue(TabBarForegroundColorProperty, p.Accent);
        SetValue(TabBarTitleColorProperty, p.Accent);
        SetValue(TabBarUnselectedColorProperty, p.Muted);
        SetValue(ForegroundColorProperty, p.TextPrimary);
        SetValue(TitleColorProperty, p.TextPrimary);
    }
}

