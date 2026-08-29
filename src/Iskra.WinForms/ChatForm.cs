using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;

namespace Iskra.WinForms;

public sealed class ChatForm : Form
{
    private readonly AuthService _auth;
    private readonly ChatRepository _chats;
    private readonly MessengerServerSyncService _sync;
    private readonly ChatEntity _chat;
    private readonly ILogger _logger;
    private readonly ListBox _messages = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox _input = new() { Dock = DockStyle.Fill };
    private readonly Button _send = new() { Text = "Отправить", Width = 100 };

    public ChatForm(
        AuthService auth,
        ChatRepository chats,
        MessengerServerSyncService sync,
        ChatEntity chat,
        ILogger logger)
    {
        _auth = auth;
        _chats = chats;
        _sync = sync;
        _chat = chat;
        _logger = logger;
        Text = $"{chat.PeerNickname} ({chat.PeerNetworkIdShort})";
        Width = 560;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;

        _send.Click += async (_, _) => await SendAsync().ConfigureAwait(true);
        AcceptButton = _send;

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 40, ColumnCount = 2 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_input, 0, 0);
        bottom.Controls.Add(_send, 1, 0);

        Controls.Add(_messages);
        Controls.Add(bottom);

        Load += async (_, _) => await ReloadAsync().ConfigureAwait(true);
        _chats.ChatMessageAppended += OnAppended;
        FormClosed += (_, _) => _chats.ChatMessageAppended -= OnAppended;
    }

    private void OnAppended(object? sender, ChatMessageAppendedEventArgs e)
    {
        if (e.ChatId != _chat.Id)
            return;
        if (IsHandleCreated)
            BeginInvoke(new Action(() => _ = ReloadAsync()));
    }

    private async Task ReloadAsync()
    {
        var rows = await _chats.ListMessagesAsync(_chat.Id).ConfigureAwait(true);
        _messages.Items.Clear();
        foreach (var m in rows)
        {
            var who = m.Outgoing ? "я" : _chat.PeerNickname;
            _messages.Items.Add($"{who}: {m.Text}");
        }

        if (_messages.Items.Count > 0)
            _messages.SelectedIndex = _messages.Items.Count - 1;
    }

    private async Task SendAsync()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0)
            return;
        var user = _auth.CurrentUser;
        if (user == null)
            return;

        _send.Enabled = false;
        try
        {
            var wire = ChatWireCodec.EncodeText(text);
            await _chats.AddMessageAsync(_chat.Id, true, text, MessageDeliveryStatus.Pending).ConfigureAwait(true);
            var ok = await _sync.TryDeliverWireAsync(_chat, user, wire).ConfigureAwait(true);
            if (!ok)
                MessageBox.Show(this, "Сервер не принял сообщение (нет доверенного сервера или пир не зарегистрирован).",
                    "Отправка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _input.Clear();
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send text");
            MessageBox.Show(this, ex.Message, "Отправка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _send.Enabled = true;
        }
    }
}
