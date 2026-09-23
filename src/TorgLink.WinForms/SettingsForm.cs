using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.Routing;
using ShortP2P.Crypto;
using ShortP2P.Discovery;

namespace TorgLink.WinForms;

/// <summary>
/// Maui Settings subset that works without BLE/camera: profile, LAN, routing, economy, keys, about.
/// </summary>
public sealed partial class SettingsForm : AppForm
{
    private readonly AuthService _auth = null!;
    private readonly P2pRoutingSettings _live = null!;
    private readonly P2pRoutingSettingsStore _store = null!;
    private readonly IServiceProvider _services = null!;
    private readonly string _appRoot = null!;
    private readonly ILogger<SettingsForm> _logger = null!;

    public SettingsForm()
    {
        InitializeComponent();
    }

    public SettingsForm(
        AuthService auth,
        P2pRoutingSettings live,
        P2pRoutingSettingsStore store,
        IServiceProvider services,
        ILogger<SettingsForm> logger)
        : this()
    {
        _auth = auth;
        _live = live;
        _store = store;
        _services = services;
        _logger = logger;
        _appRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TorgLink", "WinForms");

        foreach (var mode in new[]
                 {
                     TrafficQualityMode.Normal, TrafficQualityMode.Economy, TrafficQualityMode.UltraEconomy
                 })
            _economy.Items.Add(new EconomyItem(mode));

        foreach (var p in LinkTechnologyPresetExtensions.AllPresets)
            _link.Items.Add(new LinkItem(p));

        _economy.SelectedIndexChanged += (_, _) => UpdateEconomyHint();

        _save.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
        _editProfile.Click += (_, _) =>
        {
            using var f = _services.GetRequiredService<ProfileForm>();
            if (f.ShowDialog(this) == DialogResult.OK)
                _ = LoadAsync();
        };
        _keys.Click += (_, _) => CopyKeys();
        _about.Click += (_, _) => MessageBox.Show(this,
            "Mesh-мессенджер.\nTorgLink.WinForms 0.1 (.NET Framework 4.8)\nWindows 7 SP1+\nБез BLE и камеры. QR — из файла.",
            "TorgLink", MessageBoxButtons.OK, MessageBoxIcon.Information);

        Load += async (_, _) => await LoadAsync().ConfigureAwait(true);
    }

    private async Task LoadAsync()
    {
        var u = _auth.CurrentUser;
        var about = u != null && !string.IsNullOrWhiteSpace(u.AboutMe)
            ? $" · {TrimAbout(u.AboutMe, 60)}"
            : "";
        _profile.Text = u == null ? "Не выполнен вход" : $"{u.Nickname}  ·  {u.NetworkIdShort}{about}";
        _udpPort.Text = u?.DataUdpPort.ToString() ?? "—";
        _storage.Text = FormatStorage(_appRoot);

        var s = await _store.LoadAsync().ConfigureAwait(true);
        RoutingSettingsLive.Overlay(_live, s);

        _lan.Checked = _live.EnableUdpTransport;
        _shareRoutes.Checked = _live.AdvertisedPeerCapabilities.HasFlag(PresencePeerCapabilities.PeerSearch);
        _hops.Value = Clamp(_live.MaxSearchHops, 1, 3);
        _attempts.Value = Clamp(_live.SendFailureSearchAttempts, 1, 20);
        _delayMs.Value = Clamp((decimal)_live.SendFailureRetryDelay.TotalMilliseconds, 0, 3_600_000);
        _timeoutMs.Value = Clamp((decimal)_live.SearchWaitTimeout.TotalMilliseconds, 500, 120_000);

        SelectEconomy(_live.TrafficQuality);
        var li = Array.IndexOf(LinkTechnologyPresetExtensions.AllPresets, _live.LinkTechnology);
        _link.SelectedIndex = li >= 0 ? li : 0;
        UpdateEconomyHint();
    }

    private void SelectEconomy(TrafficQualityMode mode)
    {
        for (var i = 0; i < _economy.Items.Count; i++)
        {
            if (_economy.Items[i] is EconomyItem item && item.Mode == mode)
            {
                _economy.SelectedIndex = i;
                return;
            }
        }

        _economy.SelectedIndex = 0;
    }

    private void UpdateEconomyHint()
    {
        var mode = SelectedEconomy();
        var (w, h) = mode.GetVideoResolution();
        _economyHint.Text =
            $"голос {VoiceKbps(mode)} kbit/s  ·  видео {w}×{h}, {mode.GetCameraVideoBitrate() / 1000} kbit/s";
    }

    private static int VoiceKbps(TrafficQualityMode mode) =>
        mode switch
        {
            TrafficQualityMode.UltraEconomy => 8,
            TrafficQualityMode.Economy => 12,
            _ => 24
        };

    private TrafficQualityMode SelectedEconomy() =>
        _economy.SelectedItem is EconomyItem e ? e.Mode : TrafficQualityMode.Normal;

    private async Task SaveAsync()
    {
        try
        {
            var s = await _store.LoadAsync().ConfigureAwait(true);
            s.EnableUdpTransport = _lan.Checked;
            s.EnableBluetoothTransport = false;
            s.TrafficQuality = SelectedEconomy();
            s.MaxSearchHops = (int)_hops.Value;
            s.SendFailureSearchAttempts = (int)_attempts.Value;
            s.SendFailureRetryDelay = TimeSpan.FromMilliseconds((double)_delayMs.Value);
            s.SearchWaitTimeout = TimeSpan.FromMilliseconds((double)_timeoutMs.Value);
            if (_link.SelectedItem is LinkItem li)
                s.LinkTechnology = li.Preset;

            var cap = (s.AdvertisedPeerCapabilities & ~PresencePeerCapabilities.PeerSearch) |
                      PresencePeerCapabilities.Chat;
            if (_shareRoutes.Checked)
                cap |= PresencePeerCapabilities.PeerSearch;
            s.AdvertisedPeerCapabilities = cap;

            await _store.SaveAsync(s).ConfigureAwait(false);
            RoutingSettingsLive.Overlay(_live, s);
            _logger.LogInformation("Settings saved: udp={Udp} quality={Quality} hops={Hops}",
                s.EnableUdpTransport, s.TrafficQuality, s.MaxSearchHops);
            MessageBox.Show(this, "Сохранено.", "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Save settings");
            MessageBox.Show(this, ex.Message, "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CopyKeys()
    {
        var u = _auth.CurrentUser;
        if (u == null)
            return;
        try
        {
            var pub = RsaKeySerializer.SerializePublic(_auth.GetCurrentPublicKey());
            var text = $"Network id: {u.NetworkIdShort}\nPublic key JSON:\n{pub}";
            Clipboard.SetText(text);
            MessageBox.Show(this, "Ключи скопированы в буфер обмена.", "Настройки", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Copy keys");
            MessageBox.Show(this, ex.Message, "Настройки", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
        value < min ? min : value > max ? max : value;

    private static decimal Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;

    private static string FormatStorage(string dir)
    {
        try
        {
            if (!Directory.Exists(dir))
                return "—";
            long bytes = 0;
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                bytes += new FileInfo(f).Length;
            var mb = bytes / (1024.0 * 1024.0);
            return mb >= 1024 ? $"{mb / 1024:0.00} ГБ  ({dir})" : $"{mb:0.00} МБ  ({dir})";
        }
        catch
        {
            return "—";
        }
    }

    private static string TrimAbout(string text, int max)
    {
        var t = text.Trim();
        return t.Length <= max ? t : t[..max] + "…";
    }

    private sealed record EconomyItem(TrafficQualityMode Mode)
    {
        public override string ToString() => Mode switch
        {
            TrafficQualityMode.UltraEconomy => "Ультраэкономия (144p / 8 kbit/s голос)",
            TrafficQualityMode.Economy => "Экономия (240p / 12 kbit/s голос)",
            _ => "Нормальный (480p / 24 kbit/s голос)"
        };
    }

    private sealed record LinkItem(LinkTechnologyPreset Preset)
    {
        public override string ToString() => Preset.GetDisplayLabel();
    }
}
