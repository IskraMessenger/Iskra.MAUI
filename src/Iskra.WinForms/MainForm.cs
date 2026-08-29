using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.Data;
using ShortP2P.Client.Qr;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.Crypto;

namespace Iskra.WinForms;

public sealed class MainForm : Form
{
    private readonly AuthService _auth;
    private readonly ChatRepository _chats;
    private readonly IServiceProvider _services;
    private readonly ILogger<MainForm> _logger;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label _status = new() { AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(8) };
    private List<ChatEntity> _items = [];

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
        var servers = new Button { Text = "Серверы", AutoSize = true };
        var myQr = new Button { Text = "Мой QR", AutoSize = true };
        var logout = new Button { Text = "Выйти", AutoSize = true };
        add.Click += (_, _) => OnAddChat();
        servers.Click += (_, _) =>
        {
            using var f = _services.GetRequiredService<MessengerServersForm>();
            f.ShowDialog(this);
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
        toolbar.Controls.Add(servers);
        toolbar.Controls.Add(myQr);
        toolbar.Controls.Add(logout);

        _list.DoubleClick += (_, _) => OpenSelected();
        _chats.ChatListChanged += (_, _) => BeginInvoke(new Action(() => _ = ReloadAsync()));
        _chats.ChatCreated += (_, _) => BeginInvoke(new Action(() => _ = ReloadAsync()));

        Controls.Add(_list);
        Controls.Add(_status);
        Controls.Add(toolbar);

        Load += async (_, _) => await ReloadAsync().ConfigureAwait(true);
    }

    private async Task ReloadAsync()
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;
        _items = (await _chats.ListChatsAsync(user.Id).ConfigureAwait(true)).ToList();
        _list.Items.Clear();
        foreach (var chat in _items)
            _list.Items.Add($"{chat.PeerNickname}  ({chat.PeerNetworkIdShort})");
        _status.Text = $"{user.Nickname}  id={user.NetworkIdShort}  чатов: {_items.Count}  (черновик net48, без BLE/камеры)";
    }

    private void OnAddChat()
    {
        using var f = _services.GetRequiredService<AddChatForm>();
        if (f.ShowDialog(this) != DialogResult.OK)
            return;
        _ = ReloadAsync();
    }

    private void OpenSelected()
    {
        var i = _list.SelectedIndex;
        if (i < 0 || i >= _items.Count)
            return;
        using var chat = new ChatForm(_auth, _chats, _services.GetRequiredService<MessengerServerSyncService>(),
            _items[i], _logger);
        chat.ShowDialog(this);
        _ = ReloadAsync();
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
