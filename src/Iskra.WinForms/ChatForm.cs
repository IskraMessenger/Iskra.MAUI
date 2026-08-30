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
    private ChatEntity _chat;
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

        Load += async (_, _) =>
        {
            await ReloadAsync().ConfigureAwait(true);
            try
            {
                await _sync.PublishChatRequestAsync(_chat.PeerNetworkIdShort).ConfigureAwait(true);
                await _sync.DrainInboxOnceAsync().ConfigureAwait(true);
                await RefreshChatEntityAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "PublishChatRequest on chat open");
            }
        };
        _chats.ChatMessageAppended += OnAppended;
        _chats.ChatListChanged += OnChatListChanged;
        FormClosed += (_, _) =>
        {
            _chats.ChatMessageAppended -= OnAppended;
            _chats.ChatListChanged -= OnChatListChanged;
        };
    }

    private void OnChatListChanged(object? sender, EventArgs e)
    {
        if (IsHandleCreated)
            BeginInvoke(new Action(() => _ = RefreshChatEntityAsync()));
    }

    private void OnAppended(object? sender, ChatMessageAppendedEventArgs e)
    {
        if (e.ChatId != _chat.Id)
            return;
        if (IsHandleCreated)
            BeginInvoke(new Action(() => _ = ReloadAsync()));
    }

    private async Task RefreshChatEntityAsync()
    {
        var fresh = await _chats.GetChatAsync(_chat.Id).ConfigureAwait(false);
        if (fresh == null)
            return;
        _chat = fresh;
        if (IsHandleCreated && InvokeRequired)
        {
            BeginInvoke(new Action(() =>
                Text = $"{_chat.PeerNickname} ({_chat.PeerNetworkIdShort})"));
            return;
        }

        Text = $"{_chat.PeerNickname} ({_chat.PeerNetworkIdShort})";
    }

    private async Task ReloadAsync()
    {
        var rows = await _chats.ListMessagesAsync(_chat.Id).ConfigureAwait(false);
        void Bind()
        {
            _messages.Items.Clear();
            foreach (var m in rows)
            {
                var who = m.Outgoing ? "я" : _chat.PeerNickname;
                _messages.Items.Add($"{who}: {m.Text}");
            }

            if (_messages.Items.Count > 0)
                _messages.SelectedIndex = _messages.Items.Count - 1;
        }

        if (IsHandleCreated && InvokeRequired)
            BeginInvoke(new Action(Bind));
        else
            Bind();
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
            await RefreshChatEntityAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(_chat.PeerRsaPublicJson))
            {
                await _sync.DrainInboxOnceAsync().ConfigureAwait(false);
                await RefreshChatEntityAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(_chat.PeerRsaPublicJson))
            {
                ShowSendWarning(
                    "Пока нет ключа пира. Второй клиент должен быть запущен и ответить на приглашение — после этого сообщение можно отправить. «Офлайн» в контактах не значит, что сервер выключен.");
                return;
            }

            var wire = ChatWireCodec.EncodeText(text);
            await _chats.AddMessageAsync(_chat.Id, true, text, MessageDeliveryStatus.Pending).ConfigureAwait(false);
            var ok = await _sync.TryDeliverWireAsync(_chat, user, wire).ConfigureAwait(false);
            if (!ok)
                ShowSendWarning(
                    "Сервер не принял сообщение. Проверьте «Серверы»: доверенный и активный, клиент должен быть зарегистрирован на том же сервере.");
            else
            {
                if (InvokeRequired)
                    BeginInvoke(new Action(() => _input.Clear()));
                else
                    _input.Clear();
            }

            await ReloadAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send text");
            ShowSendError(ex.Message);
        }
        finally
        {
            if (IsHandleCreated && InvokeRequired)
                BeginInvoke(new Action(() => _send.Enabled = true));
            else
                _send.Enabled = true;
        }
    }

    private void ShowSendWarning(string text)
    {
        void Show() =>
            MessageBox.Show(this, text, "Отправка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        if (InvokeRequired)
            BeginInvoke(new Action(Show));
        else
            Show();
    }

    private void ShowSendError(string text)
    {
        void Show() =>
            MessageBox.Show(this, text, "Отправка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        if (InvokeRequired)
            BeginInvoke(new Action(Show));
        else
            Show();
    }
}
