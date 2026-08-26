using Iskra.Maui.Localization;

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
        ApplyLocalizedTitles();
        ThemeService.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(ApplyChrome);
        LanguageService.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(ApplyLocalizedTitles);
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

    private void ApplyLocalizedTitles()
    {
        var chats = Loc.T("tab.chats");
        var contacts = Loc.T("tab.contacts");
        var network = Loc.T("tab.network");
        var settings = Loc.T("tab.settings");

        // TabBar reads Tab.Title (not ShellContent). Named tabs so we never miss the section.
        ChatsTab.Title = chats;
        ContactsTab.Title = contacts;
        NetworkTab.Title = network;
        SettingsTab.Title = settings;

        ChatsHost.Title = chats;
        ContactsHost.Title = contacts;
        NetworkHost.Title = network;
        SettingsHost.Title = settings;

        SetPageTitle(ChatsHost.Content, chats);
        SetPageTitle(ContactsHost.Content, contacts);
        SetPageTitle(NetworkHost.Content, network);
        SetPageTitle(SettingsHost.Content, settings);

        // Fallback walk for platforms that flatten / wrap TabBar differently
        foreach (var item in Items)
            ApplyTitlesToItem(item, chats, contacts, network, settings);
    }

    private static void SetPageTitle(object? content, string title)
    {
        if (content is Page page)
            page.Title = title;
    }

    private static void ApplyTitlesToItem(ShellItem item, string chats, string contacts, string network,
        string settings)
    {
        foreach (var section in item.Items)
            ApplyTitlesToSection(section, chats, contacts, network, settings);
    }

    private static void ApplyTitlesToSection(ShellSection section, string chats, string contacts, string network,
        string settings)
    {
        var title = MatchTabTitle(section, chats, contacts, network, settings);
        if (title != null)
            section.Title = title;

        foreach (var content in section.Items)
        {
            var contentTitle = MatchTabTitle(content, chats, contacts, network, settings) ?? title;
            if (contentTitle != null)
                content.Title = contentTitle;
        }
    }

    private static string? MatchTabTitle(BaseShellItem item, string chats, string contacts, string network,
        string settings)
    {
        // Match by route/name when available; otherwise by known RU/EN/ES/ZH titles already set.
        var key = item.Route ?? item.Title ?? "";
        if (ContainsAny(key, "chat", "чат", "聊天"))
            return chats;
        if (ContainsAny(key, "contact", "контакт", "联系"))
            return contacts;
        if (ContainsAny(key, "network", "сеть", "red", "网络"))
            return network;
        if (ContainsAny(key, "setting", "настрой", "ajust", "设置"))
            return settings;
        return null;
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
