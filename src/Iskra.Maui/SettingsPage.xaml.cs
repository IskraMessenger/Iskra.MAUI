using Microsoft.Maui.Controls.Shapes;
using Microsoft.Extensions.Logging;
using Iskra.Maui.Localization;
using Iskra.Maui.Services;
using ShortP2P.Auth;
using ShortP2P.Client.Bluetooth;
using ShortP2P.Client.Routing;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;

namespace Iskra.Maui;

public partial class SettingsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly IBluetoothTransportProvider _bluetoothTransport;
    private readonly ILogger<SettingsPage> _logger;
    private readonly UserP2pRuntime _p2p;
    private readonly P2pRoutingSettingsStore _store;
    private bool _suppressToggle;

    public SettingsPage(AuthService auth, UserP2pRuntime p2p, P2pRoutingSettingsStore store,
        IBluetoothTransportProvider bluetoothTransport, ILogger<SettingsPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _p2p = p2p;
        _store = store;
        _bluetoothTransport = bluetoothTransport;
        _logger = logger;
        LanguageService.Changed += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(async () => await LoadAsync().ConfigureAwait(true));

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task LoadAsync()
    {
        ApplyLocalizedChrome();
        var u = _auth.CurrentUser;
        Header.Bind(u, _p2p);
        if (u != null)
        {
            ProfileName.Text = u.Nickname;
            ProfileId.Text = u.NetworkIdShort;
            ProfileInitials.Text = IskraTheme.Initials(u.Nickname);
            ProfileAvatar.BackgroundColor = IskraTheme.AvatarColor(u.NetworkIdShort);
            UdpPortLabel.Text = u.DataUdpPort.ToString();
        }

        var persisted = await _store.LoadAsync().ConfigureAwait(true);
        MediaEconomy.Apply(_p2p, persisted.TrafficSavingEnabled);

        _suppressToggle = true;
        BluetoothSwitch.IsToggled = _p2p.Settings.EnableBluetoothTransport;
        LanSwitch.IsToggled = _p2p.Settings.EnableUdpTransport;
        RoutingSwitch.IsToggled = _p2p.Settings.AdvertisedPeerCapabilities.HasFlag(PresencePeerCapabilities.PeerSearch);
        EconomySwitch.IsToggled = _p2p.Settings.TrafficSavingEnabled;
        _suppressToggle = false;
        BluetoothHint.Text = BluetoothSwitch.IsToggled ? Loc.T("on") : Loc.T("off");
        LanHint.Text = LanSwitch.IsToggled ? Loc.T("on") : Loc.T("off");
        RoutingHint.Text = RoutingSwitch.IsToggled ? Loc.T("on") : Loc.T("off");
        EconomyHint.Text = EconomySwitch.IsToggled ? MediaEconomy.Hint : Loc.T("off");
        StorageLabel.Text = FormatStorage();
        RebuildLanguageChips();
        RebuildThemeChips();
        await Task.CompletedTask.ConfigureAwait(true);
    }

    private void ApplyLocalizedChrome()
    {
        Title = Loc.T("settings.title");
        LanguageSectionLabel.Text = Loc.T("lang.section");
        AppearanceLabel.Text = Loc.T("settings.appearance");
        BluetoothLabel.Text = Loc.T("settings.bluetooth");
        UdpLabel.Text = Loc.T("settings.udp");
        LanLabel.Text = Loc.T("settings.lan");
        RoutingLabel.Text = Loc.T("settings.routing");
        EconomyLabel.Text = Loc.T("settings.economy");
        StorageTitleLabel.Text = Loc.T("settings.storage");
        ExportKeysButton.Text = Loc.T("settings.export_keys");
        RoutingOpenButton.Text = Loc.T("settings.routing_open");
        ConnectionTestButton.Text = Loc.T("settings.connection_test");
        LogsButton.Text = Loc.T("settings.logs");
        AboutButton.Text = Loc.T("settings.about");
        LogoutButton.Text = Loc.T("settings.logout");
        var warn = LanguageService.TranslationWarning(LanguageService.Current);
        LanguageWarningLabel.Text = warn;
        LanguageWarningLabel.IsVisible = LanguageService.ShowTranslationWarning;
    }

    private void RebuildLanguageChips()
    {
        LanguageChips.Children.Clear();
        foreach (var lang in new[]
                 {
                     AppLanguage.Russian, AppLanguage.English, AppLanguage.Spanish, AppLanguage.ChineseSimplified
                 })
        {
            var selected = lang == LanguageService.Current;
            var btn = new Button
            {
                Text = LanguageService.NativeName(lang),
                FontSize = 13,
                BackgroundColor = selected ? IskraTheme.Accent : IskraTheme.Current.Surface,
                TextColor = selected ? IskraTheme.Current.ButtonText : IskraTheme.Text,
                Padding = new Thickness(12, 8)
            };
            var captured = lang;
            btn.Clicked += (_, _) =>
            {
                if (captured == LanguageService.Current)
                    return;
                var warn = LanguageService.TranslationWarning(captured);
                if (!string.IsNullOrEmpty(warn))
                    _ = DisplayAlert(LanguageService.NativeName(captured), warn, Loc.T("ok"));
                LanguageService.Set(captured);
            };
            LanguageChips.Children.Add(btn);
        }
    }

    private void RebuildThemeChips()
    {
        ThemeChips.Children.Clear();
        foreach (var kind in ThemeCatalog.All)
        {
            var palette = ThemeCatalog.Get(kind);
            var selected = kind == ThemeService.CurrentKind;
            var swatch = new Border
            {
                WidthRequest = 36,
                HeightRequest = 36,
                StrokeThickness = selected ? 3 : 1,
                Stroke = selected ? palette.TextPrimary : palette.Hairline,
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                BackgroundColor = palette.Accent,
                HorizontalOptions = LayoutOptions.Center
            };
            var chip = new VerticalStackLayout
            {
                Spacing = 6,
                WidthRequest = 72
            };
            chip.Children.Add(swatch);
            chip.Children.Add(new Label
            {
                Text = palette.Title,
                FontSize = 11,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = selected ? IskraTheme.Accent : IskraTheme.Muted
            });
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                ThemeService.Apply(kind);
                RebuildThemeChips();
            };
            chip.GestureRecognizers.Add(tap);
            ThemeChips.Children.Add(chip);
        }
    }

    private static string FormatStorage()
    {
        try
        {
            long bytes = 0;
            var dir = FileSystem.AppDataDirectory;
            if (Directory.Exists(dir))
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    bytes += new FileInfo(f).Length;
            var mb = bytes / (1024.0 * 1024.0);
            return mb >= 1024 ? Loc.Tf("settings.storage_gb", mb / 1024) : Loc.Tf("settings.storage_mb", mb);
        }
        catch
        {
            return "—";
        }
    }

    private async void OnBluetoothToggled(object? sender, ToggledEventArgs e)
    {
        BluetoothHint.Text = e.Value ? Loc.T("on") : Loc.T("off");
        if (!_suppressToggle)
            await SaveAsync(s => s.EnableBluetoothTransport = e.Value).ConfigureAwait(true);
    }

    private async void OnLanToggled(object? sender, ToggledEventArgs e)
    {
        LanHint.Text = e.Value ? Loc.T("on") : Loc.T("off");
        if (!_suppressToggle)
            await SaveAsync(s => s.EnableUdpTransport = e.Value).ConfigureAwait(true);
    }

    private async void OnRoutingToggled(object? sender, ToggledEventArgs e)
    {
        RoutingHint.Text = e.Value ? Loc.T("on") : Loc.T("off");
        if (_suppressToggle)
            return;
        await SaveAsync(s =>
        {
            var cap = (s.AdvertisedPeerCapabilities & ~PresencePeerCapabilities.PeerSearch) |
                      PresencePeerCapabilities.Chat;
            if (e.Value)
                cap |= PresencePeerCapabilities.PeerSearch;
            s.AdvertisedPeerCapabilities = cap;
        }).ConfigureAwait(true);
    }

    private async void OnEconomyToggled(object? sender, ToggledEventArgs e)
    {
        EconomyHint.Text = e.Value ? MediaEconomy.Hint : Loc.T("off");
        MediaEconomy.Apply(_p2p, e.Value);
        if (!_suppressToggle)
            await SaveAsync(s => s.TrafficSavingEnabled = e.Value).ConfigureAwait(true);
    }

    private async Task SaveAsync(Action<P2pRoutingSettings> mutate)
    {
        try
        {
            var s = await _store.LoadAsync().ConfigureAwait(true);
            mutate(s);
            await _store.SaveAsync(s).ConfigureAwait(true);
            AppLog.SettingChanged("EnableBluetoothTransport", s.EnableBluetoothTransport);
            AppLog.SettingChanged("EnableUdpTransport", s.EnableUdpTransport);
            AppLog.SettingChanged("AdvertisedPeerCapabilities", s.AdvertisedPeerCapabilities);
            AppLog.SettingChanged("UltraEconomy", s.TrafficSavingEnabled);
            _p2p.Settings.EnableUdpTransport = s.EnableUdpTransport;
            _p2p.Settings.EnableBluetoothTransport = s.EnableBluetoothTransport;
            _p2p.Settings.AdvertisedPeerCapabilities = s.AdvertisedPeerCapabilities | PresencePeerCapabilities.Chat;
            MediaEconomy.Apply(_p2p, s.TrafficSavingEnabled);
            _bluetoothTransport.ApplySettings(s);
            Header.Bind(_auth.CurrentUser, _p2p);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Save settings toggle");
        }
    }

    private async void OnExportKeysClicked(object? sender, EventArgs e) =>
        await ProfileShare.CopyKeysAsync(this, _auth).ConfigureAwait(true);

    private async void OnOpenRoutingClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(MauiProgram.Services.GetRequiredService<RoutingSettingsPage>()).ConfigureAwait(true);

    private async void OnConnectionTestClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(MauiProgram.Services.GetRequiredService<LanScanPage>()).ConfigureAwait(true);

    private async void OnLogsClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(MauiProgram.Services.GetRequiredService<LogsPage>()).ConfigureAwait(true);

    private async void OnAboutClicked(object? sender, EventArgs e) =>
        await DisplayAlert("Iskra", Loc.T("settings.about_body"), Loc.T("ok")).ConfigureAwait(true);

    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        try
        {
            await _p2p.StopAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stop P2P on logout");
        }

        AppLog.Ui.LogInformation("Logout");
        await _auth.LogoutAsync().ConfigureAwait(true);
        Application.Current!.MainPage = new NavigationPage(MauiProgram.Services.GetRequiredService<LoginPage>());
    }
}
