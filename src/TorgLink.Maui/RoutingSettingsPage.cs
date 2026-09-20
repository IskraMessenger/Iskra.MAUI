using TorgLink.Maui.Localization;
using TorgLink.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Client.Bluetooth;
using ShortP2P.Client.Routing;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;
using ShortP2P.Transport;

namespace TorgLink.Maui;

public class RoutingSettingsPage : ContentPage
{
    private readonly List<BluetoothRadioInfo> _adapterRadios = [];
    private readonly Switch _advertisePeerSearch = new();
    private readonly Entry _attempts = new() { Keyboard = Keyboard.Numeric };
    private readonly Picker _bluetoothAdapter = new();
    private readonly IBluetoothRadioCatalog _bluetoothCatalog;
    private readonly IBluetoothTransportProvider _bluetoothTransport;
    private readonly Entry _delayMs = new() { Keyboard = Keyboard.Numeric };
    private readonly Switch _enableBluetoothTransport = new();
    private readonly Switch _enableUdpTransport = new();
    private readonly Picker _linkTechnology = new();
    private readonly ILogger<RoutingSettingsPage> _logger;
    private readonly Entry _maxHops = new() { Keyboard = Keyboard.Numeric };
    private readonly UserP2pRuntime _runtime;
    private readonly Entry _searchTimeoutMs = new() { Keyboard = Keyboard.Numeric };
    private readonly P2pRoutingSettingsStore _store;
    private readonly Switch _suggestBluetoothPairing = new();

    private readonly Label _maxDepthLabel = new();
    private readonly Label _attemptsLabel = new();
    private readonly Label _delayLabel = new();
    private readonly Label _timeoutLabel = new();
    private readonly Label _speedLabel = new();
    private readonly Label _udpLabel = new();
    private readonly Label _btLabel = new();
    private readonly Label _btAdapterLabel = new();
    private readonly Label _btPairLabel = new();
    private readonly Label _shareRoutesLabel = new();
    private readonly Button _saveButton = new();

    private TrafficQualityMode _trafficQuality = TrafficQualityMode.Normal;

    public RoutingSettingsPage(P2pRoutingSettingsStore store, UserP2pRuntime runtime,
        IBluetoothRadioCatalog bluetoothCatalog, IBluetoothTransportProvider bluetoothTransport,
        ILogger<RoutingSettingsPage> logger)
    {
        _store = store;
        _runtime = runtime;
        _bluetoothCatalog = bluetoothCatalog;
        _bluetoothTransport = bluetoothTransport;
        _logger = logger;
        foreach (var p in LinkTechnologyPresetExtensions.AllPresets)
            _linkTechnology.Items.Add(p.GetDisplayLabel());
        _saveButton.Command = new Command(async () => await SaveAsync());
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 12,
                Children =
                {
                    _maxDepthLabel,
                    _maxHops,
                    _attemptsLabel,
                    _attempts,
                    _delayLabel,
                    _delayMs,
                    _timeoutLabel,
                    _searchTimeoutMs,
                    _speedLabel,
                    _linkTechnology,
                    _udpLabel,
                    _enableUdpTransport,
                    _btLabel,
                    _enableBluetoothTransport,
                    _btAdapterLabel,
                    _bluetoothAdapter,
                    _btPairLabel,
                    _suggestBluetoothPairing,
                    _shareRoutesLabel,
                    _advertisePeerSearch,
                    _saveButton
                }
            }
        };
        ApplyLocalizedUi();
    }

    protected override async void OnAppearing()
    {
        try
        {
            base.OnAppearing();
            ApplyLocalizedUi();
            var s = await _store.LoadAsync().ConfigureAwait(true);
            _maxHops.Text = s.MaxSearchHops.ToString();
            _attempts.Text = s.SendFailureSearchAttempts.ToString();
            _delayMs.Text = ((int)s.SendFailureRetryDelay.TotalMilliseconds).ToString();
            _searchTimeoutMs.Text = ((int)s.SearchWaitTimeout.TotalMilliseconds).ToString();
            var idx = Array.IndexOf(LinkTechnologyPresetExtensions.AllPresets, s.LinkTechnology);
            _linkTechnology.SelectedIndex = idx >= 0 ? idx : 0;
            _trafficQuality = s.TrafficQuality;
            _enableUdpTransport.IsToggled = s.EnableUdpTransport;
            _enableBluetoothTransport.IsToggled = s.EnableBluetoothTransport;
            _suggestBluetoothPairing.IsToggled = s.SuggestBluetoothPairing;
            _advertisePeerSearch.IsToggled = s.AdvertisedPeerCapabilities.HasFlag(PresencePeerCapabilities.PeerSearch);
            await LoadBluetoothAdaptersAsync(s).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Load P2P routing settings");
        }
    }

    private void ApplyLocalizedUi()
    {
        Title = Loc.T("routing.title");
        _maxDepthLabel.Text = Loc.T("routing.max_depth");
        _attemptsLabel.Text = Loc.T("routing.attempts");
        _delayLabel.Text = Loc.T("routing.delay");
        _timeoutLabel.Text = Loc.T("routing.timeout");
        _speedLabel.Text = Loc.T("routing.speed");
        _udpLabel.Text = Loc.T("routing.udp");
        _btLabel.Text = Loc.T("routing.bt");
        _btAdapterLabel.Text = Loc.T("routing.bt_adapter");
        _btPairLabel.Text = Loc.T("routing.bt_pair");
        _shareRoutesLabel.Text = Loc.T("routing.share_routes");
        _saveButton.Text = Loc.T("save");
        _maxHops.Placeholder = Loc.T("routing.depth_ph");
    }

    private async Task LoadBluetoothAdaptersAsync(P2pRoutingSettings settings)
    {
        _bluetoothAdapter.Items.Clear();
        _adapterRadios.Clear();
        try
        {
            var radios = await _bluetoothCatalog.ListRadiosAsync().ConfigureAwait(true);
            _adapterRadios.AddRange(radios);
            foreach (var r in radios)
            {
                var suffix = r.IsDefault ? Loc.T("routing.default") : string.Empty;
                _bluetoothAdapter.Items.Add($"{r.DisplayName} ({r.MacString}){suffix}");
            }

            var pick = 0;
            if (!string.IsNullOrWhiteSpace(settings.SelectedBluetoothAdapterDeviceId))
            {
                var i = _adapterRadios.FindIndex(r =>
                    r.DeviceId == settings.SelectedBluetoothAdapterDeviceId);
                if (i >= 0)
                    pick = i;
            }
            else
            {
                var def = _adapterRadios.FindIndex(r => r.IsDefault);
                if (def >= 0)
                    pick = def;
            }

            if (_bluetoothAdapter.Items.Count > 0)
                _bluetoothAdapter.SelectedIndex = pick;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not list Bluetooth adapters");
            _bluetoothAdapter.Items.Add(Loc.T("routing.adapters_unavailable"));
            _bluetoothAdapter.SelectedIndex = 0;
        }
    }

    private void ApplySelectedAdapter(P2pRoutingSettings s)
    {
        var sel = _bluetoothAdapter.SelectedIndex;
        if (_adapterRadios.Count == 0 || sel < 0 || sel >= _adapterRadios.Count)
        {
            s.SelectedBluetoothAdapterDeviceId = null;
            s.SelectedBluetoothAdapterMac = null;
            return;
        }

        var r = _adapterRadios[sel];
        s.SelectedBluetoothAdapterDeviceId = r.DeviceId;
        s.SelectedBluetoothAdapterMac = r.MacString;
    }

    private async Task SaveAsync()
    {
        if (!int.TryParse(_maxHops.Text, out var mh) || mh is < 1 or > 3)
        {
            await DisplayAlert(Loc.T("error"), Loc.T("routing.err_depth"), Loc.T("ok")).ConfigureAwait(true);
            return;
        }

        if (!int.TryParse(_attempts.Text, out var at) || at < 1)
        {
            await DisplayAlert(Loc.T("error"), Loc.T("routing.err_attempts"), Loc.T("ok")).ConfigureAwait(true);
            return;
        }

        if (!int.TryParse(_delayMs.Text, out var dm) || dm < 0)
        {
            await DisplayAlert(Loc.T("error"), Loc.T("routing.err_delay"), Loc.T("ok")).ConfigureAwait(true);
            return;
        }

        if (!int.TryParse(_searchTimeoutMs.Text, out var st) || st < 500)
        {
            await DisplayAlert(Loc.T("error"), Loc.T("routing.err_timeout"), Loc.T("ok")).ConfigureAwait(true);
            return;
        }

        var li = _linkTechnology.SelectedIndex;
        if (li < 0 || li >= LinkTechnologyPresetExtensions.AllPresets.Length)
            li = 0;

        var cap = (_runtime.Settings.AdvertisedPeerCapabilities & ~PresencePeerCapabilities.PeerSearch) |
                  PresencePeerCapabilities.Chat;
        if (_advertisePeerSearch.IsToggled)
            cap |= PresencePeerCapabilities.PeerSearch;
        var settings = new P2pRoutingSettings
        {
            MaxSearchHops = mh,
            SendFailureSearchAttempts = at,
            SendFailureRetryDelay = TimeSpan.FromMilliseconds(dm),
            SearchWaitTimeout = TimeSpan.FromMilliseconds(st),
            LinkTechnology = LinkTechnologyPresetExtensions.AllPresets[li],
            TrafficQuality = _trafficQuality,
            EnableUdpTransport = _enableUdpTransport.IsToggled,
            EnableBluetoothTransport = _enableBluetoothTransport.IsToggled,
            SuggestBluetoothPairing = _suggestBluetoothPairing.IsToggled,
            AdvertisedPeerCapabilities = cap
        };
        ApplySelectedAdapter(settings);
        await _store.SaveAsync(settings).ConfigureAwait(true);
        AppLog.SettingChanged("MaxSearchHops", settings.MaxSearchHops);
        AppLog.SettingChanged("SendFailureSearchAttempts", settings.SendFailureSearchAttempts);
        AppLog.SettingChanged("SendFailureRetryDelayMs", dm);
        AppLog.SettingChanged("SearchWaitTimeoutMs", st);
        AppLog.SettingChanged("LinkTechnology", settings.LinkTechnology);
        AppLog.SettingChanged("EnableUdpTransport", settings.EnableUdpTransport);
        AppLog.SettingChanged("EnableBluetoothTransport", settings.EnableBluetoothTransport);
        AppLog.SettingChanged("SelectedBluetoothAdapter", settings.SelectedBluetoothAdapterMac);
        AppLog.SettingChanged("SuggestBluetoothPairing", settings.SuggestBluetoothPairing);
        AppLog.SettingChanged("AdvertisedPeerCapabilities", settings.AdvertisedPeerCapabilities);
        AppLog.SettingChanged("TrafficQuality", settings.TrafficQuality);
        _runtime.Settings.MaxSearchHops = settings.MaxSearchHops;
        _runtime.Settings.SendFailureSearchAttempts = settings.SendFailureSearchAttempts;
        _runtime.Settings.SendFailureRetryDelay = settings.SendFailureRetryDelay;
        _runtime.Settings.SearchWaitTimeout = settings.SearchWaitTimeout;
        _runtime.Settings.LinkTechnology = settings.LinkTechnology;
        MediaEconomy.Apply(_runtime, settings.TrafficQuality);
        _runtime.Settings.EnableUdpTransport = settings.EnableUdpTransport;
        _runtime.Settings.EnableBluetoothTransport = settings.EnableBluetoothTransport;
        _runtime.Settings.SelectedBluetoothAdapterDeviceId = settings.SelectedBluetoothAdapterDeviceId;
        _runtime.Settings.SelectedBluetoothAdapterMac = settings.SelectedBluetoothAdapterMac;
        _runtime.Settings.SuggestBluetoothPairing = settings.SuggestBluetoothPairing;
        _runtime.Settings.AdvertisedPeerCapabilities =
            settings.AdvertisedPeerCapabilities | PresencePeerCapabilities.Chat;
        _bluetoothTransport.ApplySettings(settings);
        await Navigation.PopAsync().ConfigureAwait(true);
    }
}
