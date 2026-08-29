using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.Data;
using ShortP2P.Client.Qr;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.Crypto;
using ShortP2P.Discovery;
using ShortP2P.Transport;

namespace Iskra.WinForms;

public sealed class MainForm : Form
{
    private readonly AuthService _auth;
    private readonly ChatRepository _chats;
    private readonly IServiceProvider _services;
    private readonly ILogger<MainForm> _logger;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label _status = new() { AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(8) };
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 4000 };
    private List<ChatEntity> _items = [];
    private bool _reloadBusy;
    private bool _reloadPending;

    public MainForm(
        AuthService auth,
        ChatRepository chats,
        IServiceProvider services,
        ILogger<MainForm> logger)
    {
        _auth = auth;
        _chats = chats;
        _services = services;
        _logger = logger;
        Text = "Iskra";
        Width = 640;
        Height = 480;
        StartPosition = FormStartPosition.CenterScreen;

        var add = new Button { Text = "Добавить чат", AutoSize = true };
        var lan = new Button { Text = "Контакты", AutoSize = true };
        var servers = new Button { Text = "Серверы", AutoSize = true };
        var myQr = new Button { Text = "Мой QR", AutoSize = true };
        var logout = new Button { Text = "Выйти", AutoSize = true };
        add.Click += (_, _) => OnAddChat();
        lan.Click += (_, _) => OnLanScan();
        servers.Click += (_, _) =>
        {
            using var f = _services.GetRequiredService<MessengerServersForm>();
            f.ShowDialog(this);
            ScheduleReload();
        };
        myQr.Click += OnMyQr;
        logout.Click += async (_, _) =>
        {
            await _auth.LogoutAsync().ConfigureAwait(true);
            DialogResult = DialogResult.Retry;
            Close();
        };

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        toolbar.Controls.Add(add);
        toolbar.Controls.Add(lan);
        toolbar.Controls.Add(servers);
        toolbar.Controls.Add(myQr);
        toolbar.Controls.Add(logout);

        _list.DoubleClick += (_, _) => OpenSelected();

        Controls.Add(_list);
        Controls.Add(_status);
        Controls.Add(toolbar);

        Load += OnLoad;
        Activated += (_, _) => ScheduleReload();
        FormClosed += OnFormClosed;
        _refreshTimer.Tick += (_, _) => ScheduleReload();
    }

    private void OnLoad(object? sender, EventArgs e)
    {
        _chats.ChatListChanged -= OnChatListChanged;
        _chats.ChatListChanged += OnChatListChanged;
        _chats.ChatCreated -= OnChatCreated;
        _chats.ChatCreated += OnChatCreated;
        _chats.ChatMessageAppended -= OnChatMessageAppended;
        _chats.ChatMessageAppended += OnChatMessageAppended;
        _refreshTimer.Start();
        ScheduleReload();
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        _chats.ChatListChanged -= OnChatListChanged;
        _chats.ChatCreated -= OnChatCreated;
        _chats.ChatMessageAppended -= OnChatMessageAppended;
    }

    private void OnChatListChanged(object? sender, EventArgs e) => ScheduleReload();

    private void OnChatCreated(object? sender, ChatCreatedEventArgs e) => ScheduleReload();

    private void OnChatMessageAppended(object? sender, ChatMessageAppendedEventArgs e) => ScheduleReload();

    private void ScheduleReload()
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        try
        {
            if (InvokeRequired)
                BeginInvoke(new Action(RunReloadOnUi));
            else
                RunReloadOnUi();
        }
        catch (ObjectDisposedException)
        {
            // ignore
        }
        catch (InvalidOperationException)
        {
            // handle not ready
        }
    }

    private void RunReloadOnUi()
    {
        if (_reloadBusy)
        {
            _reloadPending = true;
            return;
        }

        _ = ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        if (_reloadBusy || IsDisposed)
            return;
        _reloadBusy = true;
        try
        {
            do
            {
                _reloadPending = false;
                var user = _auth.CurrentUser;
                if (user == null)
                    return;

                int? selectedId = null;
                if (_list.SelectedIndex >= 0 && _list.SelectedIndex < _items.Count)
                    selectedId = _items[_list.SelectedIndex].Id;

                _items = (await _chats.ListChatsAsync(user.Id).ConfigureAwait(true)).ToList();
                if (IsDisposed)
                    return;

                _list.BeginUpdate();
                try
                {
                    _list.Items.Clear();
                    foreach (var chat in _items)
                        _list.Items.Add($"{chat.PeerNickname}  ({chat.PeerNetworkIdShort})");
                }
                finally
                {
                    _list.EndUpdate();
                }

                if (selectedId is int id)
                {
                    var idx = _items.FindIndex(c => c.Id == id);
                    if (idx >= 0)
                        _list.SelectedIndex = idx;
                }

                _status.Text =
                    $"{user.Nickname}  id={user.NetworkIdShort}  чатов: {_items.Count}  (черновик net48, UDP LAN, без BLE/камеры)";
            } while (_reloadPending && !IsDisposed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reload chat list");
        }
        finally
        {
            _reloadBusy = false;
        }
    }

    private void OnLanScan()
    {
        using var f = new LanScanForm(
            _auth,
            _chats,
            _services.GetRequiredService<LocalNetworkScanner>(),
            _services.GetRequiredService<MessengerServerSyncService>(),
            _services.GetRequiredService<MessengerServerManager>(),
            _services.GetRequiredService<IUdpTransportFactory>(),
            _services.GetRequiredService<P2pRoutingSettings>(),
            _services.GetRequiredService<ILogger<LanScanForm>>(),
            chat =>
            {
                using var chatForm = new ChatForm(
                    _auth, _chats, _services.GetRequiredService<MessengerServerSyncService>(), chat, _logger);
                chatForm.ShowDialog(this);
            },
            () =>
            {
                ScheduleReload();
                return Task.CompletedTask;
            });
        f.ShowDialog(this);
        ScheduleReload();
    }

    private void OnAddChat()
    {
        using var f = _services.GetRequiredService<AddChatForm>();
        if (f.ShowDialog(this) != DialogResult.OK)
            return;
        ScheduleReload();
    }

    private void OpenSelected()
    {
        var i = _list.SelectedIndex;
        if (i < 0 || i >= _items.Count)
            return;
        using var chat = new ChatForm(_auth, _chats, _services.GetRequiredService<MessengerServerSyncService>(),
            _items[i], _logger);
        chat.ShowDialog(this);
        ScheduleReload();
    }

    private void OnMyQr(object? sender, EventArgs e)
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;
        try
        {
            var payload = PeerQrService.BuildPayload(user, user.RsaPublicJson);
            var png = PeerQrService.EncodeQrPng(payload);
            using var preview = new QrPreviewForm("Мой QR", png, payload.Id);
            preview.ShowDialog(this);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "My QR");
            MessageBox.Show(this, ex.Message, "Мой QR", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
