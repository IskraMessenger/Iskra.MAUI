using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Data;
using ShortP2P.Client.Qr;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;

namespace Iskra.WinForms;

public sealed class AddChatForm : AppForm
{
    private readonly AuthService _auth;
    private readonly ChatRepository _chats;
    private readonly MessengerServerSyncService _sync;
    private readonly ILogger<AddChatForm> _logger;
    private readonly TextBox _nick = new() { Width = 360 };
    private readonly TextBox _id = new() { Width = 360 };
    private readonly TextBox _pub = new() { Width = 360, Height = 90, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly TextBox _host = new() { Width = 360 };

    public ChatEntity? CreatedChat { get; private set; }

    public AddChatForm(
        AuthService auth,
        ChatRepository chats,
        MessengerServerSyncService sync,
        ILogger<AddChatForm> logger)
    {
        _auth = auth;
        _chats = chats;
        _sync = sync;
        _logger = logger;
        Text = "Добавить чат";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var qrFile = new Button { Text = "QR из файла…", AutoSize = true };
        var save = new Button { Text = "Сохранить" };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel };
        qrFile.Click += OnQrFromFile;
        save.Click += async (_, _) => await OnSaveAsync().ConfigureAwait(true);
        CancelButton = cancel;

        var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true };
        layout.Controls.Add(new Label { Text = "Ник пира", AutoSize = true });
        layout.Controls.Add(_nick);
        layout.Controls.Add(new Label { Text = "Network id", AutoSize = true });
        layout.Controls.Add(_id);
        layout.Controls.Add(new Label { Text = "RSA public JSON", AutoSize = true });
        layout.Controls.Add(_pub);
        layout.Controls.Add(new Label { Text = "Host / id (необязательно)", AutoSize = true });
        layout.Controls.Add(_host);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        buttons.Controls.Add(qrFile);
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
    }

    private void OnQrFromFile(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Title = "QR-код пира",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            var bytes = File.ReadAllBytes(dlg.FileName);
            if (!PeerQrService.TryDecodeImage(bytes, out var payload, out var err) || payload == null)
            {
                MessageBox.Show(this, err ?? "QR не распознан.", "QR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _nick.Text = payload.N;
            _id.Text = payload.Id;
            _pub.Text = payload.K;
            _host.Text = payload.GetCommaSeparatedHosts();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QR from file");
            MessageBox.Show(this, ex.Message, "QR", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task OnSaveAsync()
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;
        var id = _id.Text.Trim();
        var pub = _pub.Text.Trim();
        if (id.Length == 0 || pub.Length == 0)
        {
            MessageBox.Show(this, "Нужны network id и публичный ключ.", "Добавить чат", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var chat = await _chats.AddChatAsync(
                user.Id,
                _nick.Text.Trim(),
                id,
                pub,
                string.IsNullOrWhiteSpace(_host.Text) ? id : _host.Text.Trim(),
                user.DataUdpPort,
                remote: false,
                keySource: PeerKeySource.Qr()).ConfigureAwait(true);
            await _sync.PublishChatRequestAsync(chat.PeerNetworkIdShort).ConfigureAwait(true);
            CreatedChat = chat;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Add chat");
            MessageBox.Show(this, ex.Message, "Добавить чат", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
