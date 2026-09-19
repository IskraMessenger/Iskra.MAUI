using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;

namespace Iskra.WinForms;

public sealed class ChatForm : Form
{
    private readonly AuthService _auth;
    private readonly ChatRepository _chats;
    private readonly ChatSessionCache _sessions;
    private readonly MessengerServerSyncService _sync;
    private ChatEntity _chat;
    private readonly ILogger _logger;
    private static readonly Color PeerMessageColor = Color.FromArgb(0x00, 0x99, 0x99);

    private ChatP2PSession? _p2pSession;

    private readonly ListBox _messages = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        DrawMode = DrawMode.OwnerDrawFixed
    };
    private readonly TextBox _input = new() { Dock = DockStyle.Fill };
    private readonly Button _send = new() { Text = "Отправить", Width = 100 };

    public ChatForm(
        AuthService auth,
        ChatRepository chats,
        ChatSessionCache sessions,
        MessengerServerSyncService sync,
        ChatEntity chat,
        ILogger logger)
    {
        _auth = auth;
        _chats = chats;
        _sessions = sessions;
        _sync = sync;
        _chat = chat;
        _logger = logger;
        Text = $"{chat.PeerNickname} ({chat.PeerNetworkIdShort})";
        Width = 560;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;

        _send.Click += async (_, _) => await SendAsync().ConfigureAwait(true);
        AcceptButton = _send;
        _messages.DrawItem += OnMessagesDrawItem;

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 40, ColumnCount = 2 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_input, 0, 0);
        bottom.Controls.Add(_send, 1, 0);

        Controls.Add(_messages);
        Controls.Add(bottom);

        Load += async (_, _) => await OnLoadAsync().ConfigureAwait(true);
        _chats.ChatMessageAppended += OnRepoMessagesChanged;
        _chats.ChatMessageDeliveryChanged += OnRepoMessagesChanged;
        _chats.ChatListChanged += OnChatListChanged;
        FormClosed += (_, _) =>
        {
            if (_p2pSession != null)
            {
                _p2pSession.MessagesChanged -= OnP2pMessagesChanged;
                _p2pSession = null;
            }

            _chats.ChatMessageAppended -= OnRepoMessagesChanged;
            _chats.ChatMessageDeliveryChanged -= OnRepoMessagesChanged;
            _chats.ChatListChanged -= OnChatListChanged;
        };
    }

    private async Task OnLoadAsync()
    {
        var user = _auth.CurrentUser;
        if (user != null)
            EnsureSession(user);

        await ReloadAsync().ConfigureAwait(true);

        if (_p2pSession == null)
            return;

        try
        {
            if (!_sessions.IsStarted(_chat.Id))
            {
                await _p2pSession.StartAsync().ConfigureAwait(true);
                _sessions.MarkStarted(_chat.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Chat session start for chat {ChatId}", _chat.Id);
        }

        await RefreshChatEntityAsync().ConfigureAwait(true);
    }

    private void EnsureSession(UserEntity user)
    {
        var uiSync = SynchronizationContext.Current;
        var logger = _logger;
        var sync = _sync;
        _p2pSession = _sessions.GetSession(
            _chat.Id,
            () => new ChatP2PSession(_chat, user, _chats, sync, uiSync, logger),
            s => s.ApplyChatRow(_chat));
        _p2pSession.MessagesChanged -= OnP2pMessagesChanged;
        _p2pSession.MessagesChanged += OnP2pMessagesChanged;
    }

    private void OnChatListChanged(object? sender, EventArgs e)
    {
        if (IsHandleCreated)
            BeginInvoke(new Action(() => _ = RefreshChatEntityAsync()));
    }

    private void OnRepoMessagesChanged(object? sender, ChatMessageAppendedEventArgs e)
    {
        if (e.ChatId != _chat.Id)
            return;
        ScheduleReload();
    }

    private void OnP2pMessagesChanged(object? sender, EventArgs e) => ScheduleReload();

    private void ScheduleReload()
    {
        if (IsHandleCreated)
            BeginInvoke(new Action(() => _ = ReloadAsync()));
    }

    private async Task RefreshChatEntityAsync()
    {
        var fresh = await _chats.GetChatAsync(_chat.Id).ConfigureAwait(false);
        if (fresh == null)
            return;
        _chat = fresh;
        _p2pSession?.ApplyChatRow(fresh);
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
                var status = m.Outgoing ? FormatDeliverySuffix(m.DeliveryStatus) : "";
                _messages.Items.Add(new ChatLine($"{who}: {m.Text}{status}", m.Outgoing));
            }

            if (_messages.Items.Count > 0)
                _messages.SelectedIndex = _messages.Items.Count - 1;
        }

        if (IsHandleCreated && InvokeRequired)
            BeginInvoke(new Action(Bind));
        else
            Bind();
    }

    private static string FormatDeliverySuffix(int deliveryStatus) =>
        (MessageDeliveryStatus)deliveryStatus switch
        {
            MessageDeliveryStatus.Pending => " ⌛",
            MessageDeliveryStatus.Sent => " ✓",
            MessageDeliveryStatus.Delivered => " ✓✓",
            MessageDeliveryStatus.Failed => " !",
            _ => ""
        };

    private async Task SendAsync()
    {
        var text = _input.Text.Trim();
        if (text.Length == 0)
            return;
        var user = _auth.CurrentUser;
        if (user == null)
            return;

        EnsureSession(user);

        _send.Enabled = false;
        try
        {
            // UI only enqueues Pending into DB; the session flush worker delivers from DB.
            await _p2pSession!.SendTextAsync(text).ConfigureAwait(false);
            if (InvokeRequired)
                BeginInvoke(new Action(() => _input.Clear()));
            else
                _input.Clear();
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

    private void ShowSendError(string text)
    {
        void Show() =>
            MessageBox.Show(this, text, "Отправка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        if (InvokeRequired)
            BeginInvoke(new Action(Show));
        else
            Show();
    }

    private void OnMessagesDrawItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _messages.Items.Count)
            return;

        var line = _messages.Items[e.Index] as ChatLine;
        var text = line?.Text ?? _messages.Items[e.Index]?.ToString() ?? "";
        var color = line is { Outgoing: false } ? PeerMessageColor : e.ForeColor;
        var font = e.Font ?? _messages.Font;
        var bounds = new Rectangle(e.Bounds.X + 2, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, font, bounds, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPadding);
        e.DrawFocusRectangle();
    }

    private sealed class ChatLine(string text, bool outgoing)
    {
        public string Text { get; } = text;
        public bool Outgoing { get; } = outgoing;

        public override string ToString() => Text;
    }
}
