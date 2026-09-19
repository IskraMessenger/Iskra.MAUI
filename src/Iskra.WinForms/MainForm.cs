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
    private readonly ListBox _list = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        DrawMode = DrawMode.OwnerDrawFixed
    };
    private readonly Label _status = new() { AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(8) };
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 4000 };
    private List<ChatEntity> _items = [];
    private readonly HashSet<int> _unreadChatIds = [];
    private bool _reloadBusy;
    private bool _reloadPending;

    private readonly Dictionary<int, ChatForm> _openChats = new();
    private int? _pendingOpenChatId;

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
        var settings = new Button { Text = "Настройки", AutoSize = true };
        var profile = new Button { Text = "Мой профиль", AutoSize = true };
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
        settings.Click += (_, _) =>
        {
            using var f = _services.GetRequiredService<SettingsForm>();
            f.ShowDialog(this);
            ScheduleReload();
        };
        profile.Click += (_, _) =>
        {
            using var f = _services.GetRequiredService<ProfileForm>();
            if (f.ShowDialog(this) == DialogResult.OK)
                ScheduleReload();
        };
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
        toolbar.Controls.Add(settings);
        toolbar.Controls.Add(profile);
        toolbar.Controls.Add(logout);

        _list.DoubleClick += (_, _) => OpenSelected();
        _list.DrawItem += OnDrawChatItem;
        _list.ItemHeight = Math.Max(_list.Font.Height + 4, 18);

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
        _chats.IncomingChatInvite -= OnIncomingChatInvite;
        _chats.IncomingChatInvite += OnIncomingChatInvite;
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
        _chats.IncomingChatInvite -= OnIncomingChatInvite;
        _chats.ChatMessageAppended -= OnChatMessageAppended;
        foreach (var f in _openChats.Values.ToList())
        {
            try
            {
                f.Close();
            }
            catch
            {
                // ignore
            }
        }
    }

    private void OnChatListChanged(object? sender, EventArgs e) => ScheduleReload();

    private void OnChatCreated(object? sender, ChatCreatedEventArgs e)
    {
        _pendingOpenChatId = e.ChatId;
        ScheduleReload();
    }

    private void OnIncomingChatInvite(object? sender, ChatCreatedEventArgs e)
    {
        _pendingOpenChatId = e.ChatId;
        ScheduleReload();
        ScheduleOpenChat(e.ChatId);
    }

    private void OnChatMessageAppended(object? sender, ChatMessageAppendedEventArgs e)
    {
        if (!e.Outgoing)
            MarkChatUnread(e.ChatId);
        ScheduleReload();
    }

    private void MarkChatUnread(int chatId)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        void Mark()
        {
            if (_openChats.ContainsKey(chatId))
                return;
            _unreadChatIds.Add(chatId);
            _list.Invalidate();
        }

        try
        {
            if (InvokeRequired)
                BeginInvoke(Mark);
            else
                Mark();
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
        if (IsDisposed)
            return;
        if (_reloadBusy)
        {
            _reloadPending = true;
            return;
        }

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
                void CaptureSelection()
                {
                    if (_list.SelectedIndex >= 0 && _list.SelectedIndex < _items.Count)
                        selectedId = _items[_list.SelectedIndex].Id;
                }

                if (InvokeRequired)
                    Invoke(CaptureSelection);
                else
                    CaptureSelection();

                var items = (await _chats.ListChatsAsync(user.Id).ConfigureAwait(false)).ToList();
                if (IsDisposed)
                    return;

                void Bind()
                {
                    if (IsDisposed)
                        return;
                    _items = items;
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
                    else if (_pendingOpenChatId is int pending)
                    {
                        var idx = _items.FindIndex(c => c.Id == pending);
                        if (idx >= 0)
                            _list.SelectedIndex = idx;
                    }

                    var about = string.IsNullOrWhiteSpace(user.AboutMe) ? "" : $" · {TrimAbout(user.AboutMe, 40)}";
                    _status.Text =
                        $"{user.Nickname}  id={user.NetworkIdShort}{about}  чатов: {_items.Count}  (черновик net48, UDP LAN, без BLE/камеры)";
                }

                if (InvokeRequired)
                    Invoke(Bind);
                else
                    Bind();
            } while (_reloadPending && !IsDisposed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reload chat list");
        }
        finally
        {
            _reloadBusy = false;
            if (_reloadPending && !IsDisposed)
                ScheduleReload();
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
            chat => OpenChat(chat),
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
        if (f.CreatedChat != null)
            OpenChat(f.CreatedChat);
    }

    private void OpenSelected()
    {
        var i = _list.SelectedIndex;
        if (i < 0 || i >= _items.Count)
            return;
        OpenChat(_items[i]);
    }

    private void ScheduleOpenChat(int chatId)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        try
        {
            if (InvokeRequired)
                BeginInvoke(new Action(() => _ = OpenChatByIdAsync(chatId)));
            else
                _ = OpenChatByIdAsync(chatId);
        }
        catch (ObjectDisposedException)
        {
            // ignore
        }
        catch (InvalidOperationException)
        {
            // ignore
        }
    }

    private async Task OpenChatByIdAsync(int chatId)
    {
        var chat = await _chats.GetChatAsync(chatId).ConfigureAwait(true);
        if (chat == null || IsDisposed)
            return;
        OpenChat(chat);
    }

    private void OpenChat(ChatEntity chat)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OpenChat(chat)));
            return;
        }

        _unreadChatIds.Remove(chat.Id);
        _list.Invalidate();

        if (_openChats.TryGetValue(chat.Id, out var existing) && existing is { IsDisposed: false })
        {
            existing.BringToFront();
            existing.Activate();
            return;
        }

        var form = new ChatForm(
            _auth,
            _chats,
            _services.GetRequiredService<ChatSessionCache>(),
            _services.GetRequiredService<MessengerServerSyncService>(),
            chat,
            _logger);
        _openChats[chat.Id] = form;
        form.FormClosed += (_, _) => _openChats.Remove(chat.Id);
        form.Show(this);
        ScheduleReload();
    }

    private void OnDrawChatItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _items.Count)
            return;

        var chat = _items[e.Index];
        var text = e.Index < _list.Items.Count
            ? _list.Items[e.Index]?.ToString() ?? chat.PeerNickname
            : $"{chat.PeerNickname}  ({chat.PeerNetworkIdShort})";
        var emphasize = _unreadChatIds.Contains(chat.Id);
        var baseFont = e.Font ?? _list.Font;
        using var drawFont = emphasize ? new Font(baseFont, FontStyle.Bold) : null;
        var font = drawFont ?? baseFont;
        TextRenderer.DrawText(e.Graphics, text, font, e.Bounds, e.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        e.DrawFocusRectangle();
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

    private static string TrimAbout(string text, int max)
    {
        var t = text.Trim();
        return t.Length <= max ? t : t[..max] + "…";
    }
}
