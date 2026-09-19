using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.Discovery;

namespace Iskra.WinForms;

public sealed partial class ChatForm : AppForm
{
    private readonly AuthService _auth;
    private readonly ChatRepository _chats;
    private readonly ChatSessionCache _sessions;
    private readonly MessengerServerSyncService _sync;
    private readonly ChatMediaOptions _media;
    private readonly P2pRoutingSettings _routing;
    private ChatEntity _chat;
    private readonly ILogger _logger;
    private static readonly Color PeerMessageColor = Color.FromArgb(0x00, 0x99, 0x99);
    private static readonly Color PreviewColor = Color.FromArgb(0x66, 0x66, 0x66);
    private readonly ConcurrentDictionary<int, byte> _binaryDownloadsInFlight = new();
    private readonly ToolTip _buttonTooltips = new() { ShowAlways = true };

    private ChatP2PSession? _p2pSession;
    private List<SidebarRow> _sidebarItems = [];
    private readonly HashSet<int> _unreadChatIds = [];
    private bool _sidebarSelectSuppressed;
    private bool _switchBusy;
    private bool _splitterInitialized;
    private readonly SplitContainer _split;
    private const int SidebarPreferredWidth = 220;
    private const int SidebarMinWidth = 140;
    private const int MessagesMinWidth = 240;

    private readonly ListBox _sidebar = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        DrawMode = DrawMode.OwnerDrawFixed
    };
    private readonly ListBox _messages = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        DrawMode = DrawMode.OwnerDrawFixed
    };
    private readonly TextBox _input = new() { Dock = DockStyle.Fill };
    private readonly Button _attachVoice = new()
    {
        Text = "🎤",
        Width = 36,
        Height = 32,
        Font = new Font("Segoe UI Emoji", 11f)
    };
    private readonly Button _attachImage = new()
    {
        Text = "🖼",
        Width = 36,
        Height = 32,
        Font = new Font("Segoe UI Emoji", 11f)
    };
    private readonly Button _attachDocument = new()
    {
        Text = "📄",
        Width = 36,
        Height = 32,
        Font = new Font("Segoe UI Emoji", 11f)
    };
    private readonly Button _send = new() { Text = "Отправить", Width = 120, Height = 32 };

    public int ActiveChatId => _chat.Id;

    /// <summary>Raised after the form successfully switches to another chat (sidebar or <see cref="SwitchToChatAsync"/>).</summary>
    public event EventHandler<ChatSwitchedEventArgs>? ActiveChatChanged;

    /// <summary>
    /// Raised when the user picks another chat in the sidebar. Set <see cref="ChatSwitchRequestEventArgs.Handled"/>
    /// to true to cancel the in-window switch (e.g. another ChatForm already shows that chat).
    /// </summary>
    public event EventHandler<ChatSwitchRequestEventArgs>? ChatSwitchRequested;

    public ChatForm(
        AuthService auth,
        ChatRepository chats,
        ChatSessionCache sessions,
        MessengerServerSyncService sync,
        ChatMediaOptions media,
        P2pRoutingSettings routing,
        ChatEntity chat,
        ILogger logger)
    {
        _auth = auth;
        _chats = chats;
        _sessions = sessions;
        _sync = sync;
        _media = media;
        _routing = routing;
        _chat = chat;
        _logger = logger;
        Text = FormatTitle(chat);
        Width = 780;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;

        _send.Click += async (_, _) => await SendAsync().ConfigureAwait(true);
        _attachVoice.Click += (_, _) => OnAttachVoice();
        _attachImage.Click += async (_, _) => await OnAttachImageAsync().ConfigureAwait(true);
        _attachDocument.Click += async (_, _) => await OnAttachDocumentAsync().ConfigureAwait(true);
        AcceptButton = _send;
        _messages.DrawItem += OnMessagesDrawItem;
        _messages.MouseClick += OnMessagesMouseClick;
        _messages.ItemHeight = Math.Max(_messages.Font.Height + 8, 26);

        _buttonTooltips.SetToolTip(_attachVoice,
            "Голосовое (Ogg Opus): нажмите для начала записи, ещё раз — остановить и отправить. Битрейт зависит от режима экономии трафика.");
        _buttonTooltips.SetToolTip(_attachImage, "Отправить изображение (сжатие по режиму экономии)");
        _buttonTooltips.SetToolTip(_attachDocument, "Отправить документ");
        _buttonTooltips.SetToolTip(_send, "Отправить сообщение");

        using (var bold = new Font(_sidebar.Font, FontStyle.Bold))
            _sidebar.ItemHeight = Math.Max(bold.Height + _sidebar.Font.Height + 10, 40);
        _sidebar.DrawItem += OnSidebarDrawItem;
        _sidebar.SelectedIndexChanged += OnSidebarSelectedIndexChanged;

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 44, ColumnCount = 5 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_input, 0, 0);
        bottom.Controls.Add(_attachVoice, 1, 0);
        bottom.Controls.Add(_attachImage, 2, 0);
        bottom.Controls.Add(_attachDocument, 3, 0);
        bottom.Controls.Add(_send, 4, 0);

        var right = new Panel { Dock = DockStyle.Fill };
        right.Controls.Add(_messages);
        right.Controls.Add(bottom);

        // Do not set SplitterDistance here — Width is still 0 until layout, which throws.
        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            Panel1MinSize = 0,
            Panel2MinSize = 0
        };
        _split.Panel1.Controls.Add(_sidebar);
        _split.Panel2.Controls.Add(right);
        Controls.Add(_split);

        Load += (_, _) => ApplySplitterLayout();
        Shown += (_, _) => ApplySplitterLayout();
        Resize += (_, _) => ApplySplitterLayout();
        Load += async (_, _) => await OnLoadAsync().ConfigureAwait(true);
        _chats.ChatMessageAppended += OnRepoMessagesChanged;
        _chats.ChatMessageDeliveryChanged += OnRepoMessagesChanged;
        _chats.ChatListChanged += OnChatListChanged;
        FormClosed += (_, _) =>
        {
            _voiceDiscardNextStop = true;
            CleanupVoiceRecordingHardware();

            if (_p2pSession != null)
            {
                _p2pSession.MessagesChanged -= OnP2pMessagesChanged;
                _p2pSession = null;
            }

            _chats.ChatMessageAppended -= OnRepoMessagesChanged;
            _chats.ChatMessageDeliveryChanged -= OnRepoMessagesChanged;
            _chats.ChatListChanged -= OnChatListChanged;
            _buttonTooltips.Dispose();
        };
    }

    /// <summary>Switch the open conversation in this window (reload DB messages + rebind session cache).</summary>
    public async Task SwitchToChatAsync(ChatEntity chat)
    {
        if (IsDisposed || chat.Id == _chat.Id)
            return;

        if (InvokeRequired)
        {
            await (Task)Invoke(new Func<Task>(() => SwitchToChatAsync(chat)));
            return;
        }

        if (_switchBusy)
            return;

        _switchBusy = true;
        try
        {
            var oldId = _chat.Id;
            DetachP2pMessagesChanged();

            _chat = chat;
            _unreadChatIds.Remove(chat.Id);
            Text = FormatTitle(chat);
            SelectSidebarChat(chat.Id);

            var user = _auth.CurrentUser;
            if (user != null)
                EnsureSession(user);

            await ReloadAsync().ConfigureAwait(true);

            if (_p2pSession != null && !_sessions.IsStarted(_chat.Id))
            {
                try
                {
                    await _p2pSession.StartAsync().ConfigureAwait(true);
                    _sessions.MarkStarted(_chat.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Chat session start for chat {ChatId}", _chat.Id);
                }
            }

            await RefreshChatEntityAsync().ConfigureAwait(true);
            await RefreshSidebarAsync().ConfigureAwait(true);

            ActiveChatChanged?.Invoke(this, new ChatSwitchedEventArgs(oldId, _chat.Id));
        }
        finally
        {
            _switchBusy = false;
        }
    }

    private void ApplySplitterLayout()
    {
        if (_split.IsDisposed || !_split.IsHandleCreated)
            return;

        var width = _split.ClientSize.Width;
        var splitterWidth = Math.Max(_split.SplitterWidth, 1);
        if (width <= splitterWidth + 2)
            return;

        // Keep mins within the current width so SplitterDistance never throws on shrink.
        var panel1Min = Math.Min(SidebarMinWidth, Math.Max(0, (width - splitterWidth) / 3));
        var panel2Min = Math.Min(MessagesMinWidth, Math.Max(0, (width - splitterWidth) / 2));
        if (panel1Min + panel2Min + splitterWidth > width)
        {
            panel1Min = 0;
            panel2Min = 0;
        }

        try
        {
            _split.Panel1MinSize = panel1Min;
            _split.Panel2MinSize = panel2Min;

            var maxDistance = width - panel2Min - splitterWidth;
            if (maxDistance < panel1Min)
                return;

            var desired = _splitterInitialized
                ? _split.SplitterDistance
                : SidebarPreferredWidth;
            var clamped = Math.Max(panel1Min, Math.Min(desired, maxDistance));
            if (_split.SplitterDistance != clamped)
                _split.SplitterDistance = clamped;
            _splitterInitialized = true;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Layout not ready yet (common during first show / DPI scale).
        }
    }

    private async Task OnLoadAsync()
    {
        ApplySplitterLayout();

        var user = _auth.CurrentUser;
        if (user != null)
            EnsureSession(user);

        await ReloadAsync().ConfigureAwait(true);
        await RefreshSidebarAsync().ConfigureAwait(true);

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
        DetachP2pMessagesChanged();
        _p2pSession = _sessions.GetSession(
            _chat.Id,
            () => new ChatP2PSession(_chat, user, _chats, sync, uiSync, logger),
            s => s.ApplyChatRow(_chat));
        _p2pSession.MessagesChanged += OnP2pMessagesChanged;
    }

    private void DetachP2pMessagesChanged()
    {
        if (_p2pSession == null)
            return;
        _p2pSession.MessagesChanged -= OnP2pMessagesChanged;
    }

    private void OnChatListChanged(object? sender, EventArgs e)
    {
        if (IsHandleCreated)
            BeginInvoke(new Action(() =>
            {
                _ = RefreshChatEntityAsync();
                _ = RefreshSidebarAsync();
            }));
    }

    private void OnRepoMessagesChanged(object? sender, ChatMessageAppendedEventArgs e)
    {
        if (e.ChatId == _chat.Id)
        {
            ScheduleReload();
            ScheduleSidebarRefresh();
            return;
        }

        if (!e.Outgoing)
            MarkSidebarUnread(e.ChatId);
        ScheduleSidebarRefresh();
    }

    private void OnP2pMessagesChanged(object? sender, EventArgs e) => ScheduleReload();

    private void MarkSidebarUnread(int chatId)
    {
        if (IsDisposed)
            return;
        void Mark()
        {
            if (chatId == _chat.Id)
                return;
            _unreadChatIds.Add(chatId);
            _sidebar.Invalidate();
        }

        if (IsHandleCreated && InvokeRequired)
            BeginInvoke(Mark);
        else if (IsHandleCreated)
            Mark();
    }

    private void ScheduleReload()
    {
        if (IsHandleCreated)
            BeginInvoke(new Action(() => _ = ReloadAsync()));
    }

    private void ScheduleSidebarRefresh()
    {
        if (IsHandleCreated)
            BeginInvoke(new Action(() => _ = RefreshSidebarAsync()));
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
            BeginInvoke(new Action(() => Text = FormatTitle(_chat)));
            return;
        }

        Text = FormatTitle(_chat);
    }

    private async Task RefreshSidebarAsync()
    {
        var user = _auth.CurrentUser;
        if (user == null)
            return;

        try
        {
            var list = await _chats.ListChatsAsync(user.Id).ConfigureAwait(false);
            var rows = new List<SidebarRow>(list.Count);
            foreach (var c in list)
            {
                var lastPage = await _chats
                    .ListMessagesPageDescAsync(c.Id, 0, 1, includePayloadBlob: false)
                    .ConfigureAwait(false);
                var last = lastPage.Count > 0 ? lastPage[0] : null;
                rows.Add(new SidebarRow(c, FormatPreview(last)));
            }

            void Bind()
            {
                if (IsDisposed)
                    return;
                _sidebarItems = rows;
                _sidebarSelectSuppressed = true;
                try
                {
                    _sidebar.BeginUpdate();
                    try
                    {
                        _sidebar.Items.Clear();
                        foreach (var row in _sidebarItems)
                            _sidebar.Items.Add(row);
                    }
                    finally
                    {
                        _sidebar.EndUpdate();
                    }

                    SelectSidebarChat(_chat.Id);
                }
                finally
                {
                    _sidebarSelectSuppressed = false;
                }
            }

            if (IsHandleCreated && InvokeRequired)
                BeginInvoke(new Action(Bind));
            else
                Bind();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Refresh chat sidebar");
        }
    }

    private void SelectSidebarChat(int chatId)
    {
        var idx = _sidebarItems.FindIndex(r => r.Chat.Id == chatId);
        _sidebarSelectSuppressed = true;
        try
        {
            _sidebar.SelectedIndex = idx;
        }
        finally
        {
            _sidebarSelectSuppressed = false;
        }

        _sidebar.Invalidate();
    }

    private async void OnSidebarSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_sidebarSelectSuppressed || _switchBusy)
            return;
        var i = _sidebar.SelectedIndex;
        if (i < 0 || i >= _sidebarItems.Count)
            return;

        var target = _sidebarItems[i].Chat;
        if (target.Id == _chat.Id)
        {
            _unreadChatIds.Remove(target.Id);
            _sidebar.Invalidate();
            return;
        }

        var request = new ChatSwitchRequestEventArgs(target);
        ChatSwitchRequested?.Invoke(this, request);
        if (request.Handled)
        {
            SelectSidebarChat(_chat.Id);
            return;
        }

        var fresh = await _chats.GetChatAsync(target.Id).ConfigureAwait(true);
        if (fresh == null || IsDisposed)
        {
            SelectSidebarChat(_chat.Id);
            return;
        }

        await SwitchToChatAsync(fresh).ConfigureAwait(true);
    }

    private async Task ReloadAsync()
    {
        var rows = await _chats.ListMessagesAsync(_chat.Id).ConfigureAwait(false);
        void Bind()
        {
            _messages.Items.Clear();
            foreach (var m in rows)
                _messages.Items.Add(FormatMessageLine(m));

            if (_messages.Items.Count > 0)
                _messages.SelectedIndex = _messages.Items.Count - 1;
        }

        if (IsHandleCreated && InvokeRequired)
            BeginInvoke(new Action(Bind));
        else
            Bind();
    }

    private ChatLine FormatMessageLine(ChatMessageEntity m)
    {
        var who = m.Outgoing ? "я" : _chat.PeerNickname;
        var status = m.Outgoing ? FormatDeliverySuffix(m.DeliveryStatus) : "";
        if (IsAttachmentMessage(m))
        {
            var kind = AttachmentKindLabel(m);
            var name = AttachmentDisplayName(m);
            var kb = (AttachmentSizeBytes(m) + 1023) / 1024;
            var hint = AttachmentActionHint(m);
            var body = string.IsNullOrEmpty(hint)
                ? $"[{kind}] {name} · {kb} КБ"
                : $"[{kind}] {name} · {kb} КБ · {hint}";
            return new ChatLine($"{who}: {body}{status}", m.Outgoing, m.Id, isAttachment: true);
        }

        return new ChatLine($"{who}: {m.Text}{status}", m.Outgoing, m.Id);
    }

    private static string FormatTitle(ChatEntity chat) =>
        $"{chat.PeerNickname} ({chat.PeerNetworkIdShort})";

    private static string FormatPreview(ChatMessageEntity? m)
    {
        if (m == null)
            return "";
        if (m.PayloadKind == (int)ChatPayloadKind.Image)
            return "[фото]";
        if (m.MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true)
            return "[голос]";
        if (m.PayloadKind is (int)ChatPayloadKind.File or (int)ChatPayloadKind.TransferOffer)
        {
            if (!string.IsNullOrWhiteSpace(m.TransferFileName))
                return m.TransferFileName;
            return string.IsNullOrWhiteSpace(m.Text) ? "[файл]" : m.Text;
        }

        return string.IsNullOrWhiteSpace(m.Text) ? "" : m.Text.Replace('\n', ' ');
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

    private void OnSidebarDrawItem(object? sender, DrawItemEventArgs e)
    {
        e.DrawBackground();
        if (e.Index < 0 || e.Index >= _sidebarItems.Count)
            return;

        var row = _sidebarItems[e.Index];
        var unread = _unreadChatIds.Contains(row.Chat.Id);
        var baseFont = e.Font ?? _sidebar.Font;
        using var boldFont = unread || e.Index == _sidebar.SelectedIndex
            ? new Font(baseFont, FontStyle.Bold)
            : null;
        var nameFont = boldFont ?? baseFont;
        var nameBounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 2, e.Bounds.Width - 10, nameFont.Height);
        TextRenderer.DrawText(e.Graphics, row.Chat.PeerNickname, nameFont, nameBounds, e.ForeColor,
            TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

        if (!string.IsNullOrEmpty(row.Preview))
        {
            var previewBounds = new Rectangle(
                e.Bounds.X + 6,
                e.Bounds.Y + 2 + nameFont.Height,
                e.Bounds.Width - 10,
                baseFont.Height);
            TextRenderer.DrawText(e.Graphics, row.Preview, baseFont, previewBounds, PreviewColor,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        e.DrawFocusRectangle();
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

    private void OnMessagesMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;
        var idx = _messages.IndexFromPoint(e.Location);
        if (idx < 0 || idx >= _messages.Items.Count)
            return;
        if (_messages.Items[idx] is not ChatLine { IsAttachment: true, MessageId: > 0 } line)
            return;
        _ = OpenOrDownloadAttachmentAsync(line.MessageId);
    }

    private async Task OpenOrDownloadAttachmentAsync(int messageId)
    {
        try
        {
            var row = await _chats.GetMessageAsync(messageId).ConfigureAwait(true);
            if (row == null || row.ChatId != _chat.Id)
            {
                ShowAttachmentError("Сообщение не найдено.");
                return;
            }

            if (row.ImageBlob is { Length: > 0 })
            {
                DisplayAttachment(row);
                return;
            }

            var canDownload = _p2pSession != null &&
                              !row.Outgoing &&
                              !string.IsNullOrWhiteSpace(row.TransferId);
            if (!canDownload)
            {
                ShowAttachmentError("Файл ещё недоступен для скачивания.");
                return;
            }

            QueueBinaryDownload(messageId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open attachment failed");
            ShowAttachmentError(ex.Message);
        }
    }

    private void QueueBinaryDownload(int messageId)
    {
        var session = _p2pSession;
        if (session == null)
            return;
        if (!_binaryDownloadsInFlight.TryAdd(messageId, 0))
            return;
        _ = RunBinaryDownloadAsync(session, messageId);
    }

    private async Task RunBinaryDownloadAsync(ChatP2PSession session, int messageId)
    {
        try
        {
            await session.RequestBinaryDownloadAsync(messageId).ConfigureAwait(true);
            var row = await _chats.GetMessageAsync(messageId).ConfigureAwait(true);
            if (row?.ImageBlob is not { Length: > 0 })
            {
                await ReloadAsync().ConfigureAwait(true);
                return;
            }

            var ready = row;
            void Show()
            {
                if (IsDisposed)
                    return;
                DisplayAttachment(ready);
                _ = ReloadAsync();
            }

            if (IsHandleCreated && InvokeRequired)
                BeginInvoke(new Action(Show));
            else
                Show();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Transfer download failed in chat {ChatId}", _chat.Id);
            ShowAttachmentError(ex.Message);
            await ReloadAsync().ConfigureAwait(true);
        }
        finally
        {
            _binaryDownloadsInFlight.TryRemove(messageId, out _);
        }
    }

    private void DisplayAttachment(ChatMessageEntity row)
    {
        if (row.ImageBlob is not { Length: > 0 } blob)
        {
            ShowAttachmentError("Файл ещё недоступен.");
            return;
        }

        var name = SanitizeFileName(EnsureMediaFileName(AttachmentDisplayName(row), row));
        if (IsImageAttachment(row) || IsVideoAttachment(row) || IsVoiceAttachment(row))
        {
            try
            {
                OpenWithAssociatedApp(blob, $"{row.Id}_{name}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Open media with associated app failed");
                ShowAttachmentError(ex.Message);
            }

            return;
        }

        using var sfd = new SaveFileDialog
        {
            Title = "Сохранить файл",
            FileName = name,
            Filter = "Все файлы|*.*",
            OverwritePrompt = true
        };
        if (sfd.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            File.WriteAllBytes(sfd.FileName, blob);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Save document failed");
            ShowAttachmentError(ex.Message);
        }
    }

    private static void OpenWithAssociatedApp(byte[] bytes, string fileName)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "IskraChat");
        Directory.CreateDirectory(tempDir);
        var path = Path.Combine(tempDir, fileName);
        File.WriteAllBytes(path, bytes);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void ShowAttachmentError(string text)
    {
        void Show() =>
            MessageBox.Show(this, text, "Файл", MessageBoxButtons.OK, MessageBoxIcon.Information);
        if (InvokeRequired)
            BeginInvoke(new Action(Show));
        else
            Show();
    }

    private static bool IsAttachmentMessage(ChatMessageEntity m) =>
        m.PayloadKind is (int)ChatPayloadKind.File
            or (int)ChatPayloadKind.Image
            or (int)ChatPayloadKind.TransferOffer ||
        IsVoiceAttachment(m) ||
        IsVideoAttachment(m);

    private static bool IsLocallyAvailable(ChatMessageEntity m) =>
        m.ImageBlob is { Length: > 0 } || m.HasPayloadBlob;

    private static string AttachmentActionHint(ChatMessageEntity m)
    {
        if (IsLocallyAvailable(m) || m.Outgoing)
            return "";

        return (ChatTransferState)m.TransferState switch
        {
            ChatTransferState.Transferring => "загрузка...",
            ChatTransferState.Failed => "ошибка · скачать",
            ChatTransferState.Received => "",
            _ => "скачать"
        };
    }

    private static string AttachmentKindLabel(ChatMessageEntity m)
    {
        if (IsVoiceAttachment(m))
            return "голос";
        if (IsImageAttachment(m))
            return "фото";
        if (IsVideoAttachment(m))
            return "видео";
        return "файл";
    }

    private static string AttachmentDisplayName(ChatMessageEntity m)
    {
        if (!string.IsNullOrWhiteSpace(m.TransferFileName))
            return m.TransferFileName;
        if (!string.IsNullOrWhiteSpace(m.Text))
            return m.Text;
        if (IsImageAttachment(m))
            return "image.jpg";
        if (IsVideoAttachment(m))
            return "video.mp4";
        if (IsVoiceAttachment(m))
            return "voice.ogg";
        return "file";
    }

    private static int AttachmentSizeBytes(ChatMessageEntity m) =>
        m.ImageBlob is { Length: > 0 } blob ? blob.Length : (int)m.TransferSizeBytes;

    private static bool IsVoiceAttachment(ChatMessageEntity m) =>
        string.Equals(m.TransferPayloadKind, "voice", StringComparison.OrdinalIgnoreCase) ||
        (m.MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ?? false) ||
        (m.TransferFileName?.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ?? false) ||
        (m.Text?.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool IsImageAttachment(ChatMessageEntity m) =>
        m.PayloadKind == (int)ChatPayloadKind.Image ||
        string.Equals(m.TransferPayloadKind, "image", StringComparison.OrdinalIgnoreCase) ||
        (m.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool IsVideoAttachment(ChatMessageEntity m) =>
        string.Equals(m.TransferPayloadKind, "video", StringComparison.OrdinalIgnoreCase) ||
        (m.MimeType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ?? false);

    private static string EnsureMediaFileName(string name, ChatMessageEntity row)
    {
        if (Path.HasExtension(name))
            return name;
        if (IsImageAttachment(row))
        {
            var ext = row.MimeType?.Trim().ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
            return name + ext;
        }

        if (IsVideoAttachment(row))
            return name + ".mp4";
        if (IsVoiceAttachment(row))
            return name + ".ogg";
        return name;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';

        var s = new string(chars).Trim();
        return string.IsNullOrEmpty(s) ? "document" : s;
    }

    private sealed class SidebarRow(ChatEntity chat, string preview)
    {
        public ChatEntity Chat { get; } = chat;
        public string Preview { get; } = preview;

        public override string ToString() =>
            string.IsNullOrEmpty(Preview) ? Chat.PeerNickname : $"{Chat.PeerNickname} — {Preview}";
    }

    private sealed class ChatLine(string text, bool outgoing, int messageId = 0, bool isAttachment = false)
    {
        public string Text { get; } = text;
        public bool Outgoing { get; } = outgoing;
        public int MessageId { get; } = messageId;
        public bool IsAttachment { get; } = isAttachment;

        public override string ToString() => Text;
    }
}

public sealed class ChatSwitchedEventArgs(int oldChatId, int newChatId) : EventArgs
{
    public int OldChatId { get; } = oldChatId;
    public int NewChatId { get; } = newChatId;
}

public sealed class ChatSwitchRequestEventArgs(ChatEntity chat) : EventArgs
{
    public ChatEntity Chat { get; } = chat;
    public bool Handled { get; set; }
}
