using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.Routing;
using ShortP2P.Crypto;
using ShortP2P.Discovery;

namespace Iskra.WinForms;

/// <summary>
/// Maui Settings subset that works without BLE/camera: profile, LAN, routing, economy, keys, about.
/// </summary>
public sealed class SettingsForm : Form
{
    private readonly AuthService _auth;
    private readonly P2pRoutingSettings _live;
    private readonly P2pRoutingSettingsStore _store;
    private readonly string _appRoot;
    private readonly ILogger<SettingsForm> _logger;

    private readonly Label _profile = new() { AutoSize = true };
    private readonly Label _udpPort = new() { AutoSize = true };
    private readonly CheckBox _lan = new() { Text = "LAN (UDP)", AutoSize = true };
    private readonly CheckBox _shareRoutes = new() { Text = "Делиться маршрутами (PeerSearch)", AutoSize = true };
    private readonly ComboBox _economy = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly Label _economyHint = new() { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(520, 0) };
    private readonly NumericUpDown _hops = new() { Minimum = 1, Maximum = 3, Width = 80 };
    private readonly NumericUpDown _attempts = new() { Minimum = 1, Maximum = 20, Width = 80 };
    private readonly NumericUpDown _delayMs = new() { Minimum = 0, Maximum = 3_600_000, Increment = 1000, Width = 120 };
    private readonly NumericUpDown _timeoutMs = new() { Minimum = 500, Maximum = 120_000, Increment = 500, Width = 120 };
    private readonly ComboBox _link = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly Label _storage = new() { AutoSize = true };

    public SettingsForm(
        AuthService auth,
        P2pRoutingSettings live,
        P2pRoutingSettingsStore store,
        ILogger<SettingsForm> logger)
    {
        _auth = auth;
        _live = live;
        _store = store;
        _logger = logger;
        _appRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Iskra", "WinForms");

        Text = "Настройки";
        Width = 580;
        Height = 640;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;

        foreach (var mode in new[]
                 {
                     TrafficQualityMode.Normal, TrafficQualityMode.Economy, TrafficQualityMode.UltraEconomy
                 })
            _economy.Items.Add(new EconomyItem(mode));

        foreach (var p in LinkTechnologyPresetExtensions.AllPresets)
            _link.Items.Add(new LinkItem(p));

        _economy.SelectedIndexChanged += (_, _) => UpdateEconomyHint();

        var save = new Button { Text = "Сохранить", AutoSize = true };
        var keys = new Button { Text = "Копировать ключи", AutoSize = true };
        var about = new Button { Text = "О программе", AutoSize = true };
        var close = new Button { Text = "Закрыть", DialogResult = DialogResult.OK, AutoSize = true };
        save.Click += async (_, _) => await SaveAsync().ConfigureAwait(true);
        keys.Click += (_, _) => CopyKeys();
        about.Click += (_, _) => MessageBox.Show(this,
            "Mesh-мессенджер без групп.\nIskra.WinForms 0.1 (.NET Framework 4.8)\nWindows 7 SP1+\nБез BLE и камеры. QR — из файла.",
            "Iskra", MessageBoxButtons.OK, MessageBoxIcon.Information);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoScroll = true,
            Padding = new Padding(12),
            AutoSize = true
        };

        void Add(Control c) => root.Controls.Add(c);

        Add(new Label { Text = "Профиль", Font = new Font(Font, FontStyle.Bold), AutoSize = true });
        Add(_profile);
        Add(new Label { Text = "UDP-порт данных (только просмотр)", AutoSize = true });
        Add(_udpPort);
        Add(new Label
        {
            Text = "Bluetooth в этом клиенте недоступен. Язык/тема Maui не перенесены (интерфейс на русском).",
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            MaximumSize = new Size(520, 0)
        });
        Add(new Label { Text = "Сеть", Font = new Font(Font, FontStyle.Bold), AutoSize = true });
        Add(_lan);
        Add(_shareRoutes);
        Add(new Label { Text = "Экономия трафика", AutoSize = true });
        Add(_economy);
        Add(_economyHint);
        Add(new Label { Text = "Маршрутизация", Font = new Font(Font, FontStyle.Bold), AutoSize = true });
        Add(Labeled("Макс. глубина поиска (1–3)", _hops));
        Add(Labeled("Повторы поиска при ошибке", _attempts));
        Add(Labeled("Пауза между попытками, мс", _delayMs));
        Add(Labeled("Таймаут FIND, мс", _timeoutMs));
        Add(new Label { Text = "Пресет скорости канала", AutoSize = true });
        Add(_link);
        Add(new Label { Text = "Хранилище", Font = new Font(Font, FontStyle.Bold), AutoSize = true });
        Add(_storage);

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        buttons.Controls.Add(save);
        buttons.Controls.Add(keys);
        buttons.Controls.Add(about);
        buttons.Controls.Add(close);
        Add(buttons);

        Controls.Add(root);
        AcceptButton = close;
        Load += async (_, _) => await LoadAsync().ConfigureAwait(true);
    }

    private static Control Labeled(string text, Control inner)
    {
        var p = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        p.Controls.Add(new Label { Text = text, AutoSize = true, Padding = new Padding(0, 6, 8, 0) });
        p.Controls.Add(inner);
        return p;
    }

    private async Task LoadAsync()
    {
        var u = _auth.CurrentUser;
        _profile.Text = u == null ? "Не выполнен вход" : $"{u.Nickname}  ·  {u.NetworkIdShort}";
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
        _economyHint.Text = mode.GetDisplayLabel() +
                            $"  ·  видео {w}×{h}, {mode.GetCameraVideoBitrate() / 1000} kbit/s";
    }

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

    private sealed record EconomyItem(TrafficQualityMode Mode)
    {
        public override string ToString() => Mode switch
        {
            TrafficQualityMode.UltraEconomy => "Ультраэкономия (144p / 4 kbit/s голос)",
            TrafficQualityMode.Economy => "Экономия (240p / 6 kbit/s голос)",
            _ => "Нормальный (480p / 24 kbit/s голос)"
        };
    }

    private sealed record LinkItem(LinkTechnologyPreset Preset)
    {
        public override string ToString() => Preset.GetDisplayLabel();
    }
}
