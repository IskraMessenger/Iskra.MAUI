using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using Iskra.Maui.Localization;
using Iskra.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client;
using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Data;
using ShortP2P.Client.Routing;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;

namespace Iskra.Maui;

public partial class ChatDetailPage : ContentPage
{
    private static readonly FilePickerFileType OfficeDocFileTypes = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] =
            [
                ".doc", ".docx", ".rtf", ".pdf", ".odt", ".ods", ".odp", ".odg", ".xlsx", ".xls", ".pptx", ".ppt",
                ".mp4", ".mov", ".avi", ".wmv", ".webm", ".ogv"
            ],
            [DevicePlatform.Android] =
            [
                "application/pdf",
                "application/msword",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/vnd.ms-excel",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "application/vnd.ms-powerpoint",
                "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                "application/vnd.oasis.opendocument.text",
                "application/vnd.oasis.opendocument.spreadsheet",
                "application/vnd.oasis.opendocument.presentation",
                "application/vnd.oasis.opendocument.graphics",
                "application/rtf",
                "video/mp4",
                "video/quicktime",
                "video/x-msvideo",
                "video/x-ms-wmv",
                "video/webm",
                "video/ogg"
            ],
            [DevicePlatform.iOS] = ["public.data"],
            [DevicePlatform.MacCatalyst] = ["public.data"]
        });

    private readonly AuthService _auth;
    private readonly ChatRepository _repo;
    private readonly UserP2pRuntime _p2p;
    private readonly ChatMediaOptions _media;
    private readonly P2pRoutingSettingsStore _routingStore;
    private readonly MessengerServerManager _messengerServers;
    private readonly PeerBlacklist _blacklist;
    private readonly ILogger<ChatDetailPage> _logger;
    private const int MessagesPageSize = 10;
    private readonly ObservableCollection<MessageRowVm> _messageItems = [];
    private readonly List<ChatMessageEntity> _loadedRows = [];
    private ChatP2PSession? _p2pSession;
    private ChatEntity? _chat;
    private string? _peerNetworkIdShort;
    private IDispatcherTimer? _presenceRefreshTimer;
    private bool _hasMoreRows = true;
    private bool _isLoadingRows;
    private bool _suppressLoadMore = true;
    private bool _pendingReload;
    private int _reloadEpoch;
    private int _scrollToEndEpoch;
    private VoiceRecordingSession? _voice;
    private readonly Dictionary<int, string> _attachmentDurationLabels = new();
    private readonly ConcurrentDictionary<int, byte> _binaryDownloadsInFlight = new();

    public ChatDetailPage(AuthService auth, ChatRepository repo, UserP2pRuntime p2p, ChatMediaOptions media,
        P2pRoutingSettingsStore routingStore, MessengerServerManager messengerServers, PeerBlacklist blacklist,
        ILogger<ChatDetailPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _repo = repo;
        _p2p = p2p;
        _media = media;
        _routingStore = routingStore;
        _messengerServers = messengerServers;
        _blacklist = blacklist;
        _logger = logger;
        MessagesCollection.ItemsSource = _messageItems;
    }

    public int ChatId { get; set; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var chat = await _repo.GetChatAsync(ChatId).ConfigureAwait(true);
        if (chat == null)
        {
            await DisplayAlert(Loc.T("error"), Loc.T("chat.not_found"), Loc.T("ok")).ConfigureAwait(true);
            await Navigation.PopAsync().ConfigureAwait(true);
            return;
        }

        var user = _auth.CurrentUser;
        if (user != null)
            await _blacklist.EnsureLoadedAsync(user.Id).ConfigureAwait(true);
        if (user != null && _blacklist.IsBlocked(user.Id, chat.PeerNetworkIdShort))
        {
            await Navigation.PopAsync().ConfigureAwait(true);
            return;
        }

        Title = chat.PeerNickname;
        PeerNameLabel.Text = chat.PeerNickname;
        PeerAvatarInitials.Text = IskraTheme.Initials(chat.PeerNickname);
        PeerAvatarFill.BackgroundColor = IskraTheme.AvatarColor(chat.PeerNetworkIdShort);
        PeerIdLabel.Text = Loc.Tf("chat.node", chat.PeerNetworkIdShort);
        SetControlHint(BlockPeerButton, Loc.T("blacklist.add_hint"));
        SetControlHint(ClearChatButton, Loc.T("chat.delete_hint"));
        SetControlHint(EmergencyUntrustButton, Loc.T("safety.untrust_hint"));
        MessageEntry.Placeholder = Loc.T("chat.message_ph");
        _chat = chat;
        _peerNetworkIdShort = chat.PeerNetworkIdShort;
        ActiveChatTracker.Set(chat.Id);
        RefreshSafetyLabel(chat);
        await TryRefreshPeerNicknameDisplayAsync(chat).ConfigureAwait(true);
        if (user == null)
        {
            ActiveChatTracker.Clear(chat.Id);
            _peerNetworkIdShort = null;
            _chat = null;
            await Navigation.PopAsync().ConfigureAwait(true);
            return;
        }

        // History and send must not wait for P2P handshake or peer online/offline.
        var uiSync = SynchronizationContext.Current;
        _p2pSession = _p2p.GetSession(chat, user, _auth, _repo, uiSync);
        _p2pSession.MessagesChanged += OnP2PMessagesChanged;
        _p2pSession.TransferStateChanged += OnP2PTransferStateChanged;
        _p2p.LocalScan.ClientsChanged += OnPeerLanPresenceChanged;
        _repo.PeerPublicKeyChanged += OnPeerPublicKeyChanged;
        _messengerServers.FailoverCompleted += OnMessengerServerFailover;
        EnsurePresenceRefreshTimerStarted();
        RefreshPeerPresenceLabel();
        _repo.ChatMessageAppended += OnChatMessageAppended;
        _ = ReloadMessagesAsync();
        _ = ConnectChatTransportAsync(user, chat, _p2pSession);
    }

    private async Task ConnectChatTransportAsync(UserEntity user, ChatEntity chat, ChatP2PSession session)
    {
        try
        {
            await _p2p.EnsureStartedAsync(user).ConfigureAwait(false);
            _p2p.MessengerServers?.Start();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ensure P2P (invite listener) on chat detail");
        }

        var handshake = StartChatSessionIfNeededAsync(chat, session);
        var servers = MessengerServersBootstrap.EnsureRunningAsync(_p2p, _logger);
        var publish = MessengerServersBootstrap.PublishChatRequestAsync(_p2p, chat.PeerNetworkIdShort, _logger);
        await Task.WhenAll(handshake, servers, publish).ConfigureAwait(false);
        await DrainServerInboxAsync().ConfigureAwait(false);
    }

    private async Task DrainServerInboxAsync()
    {
        var sync = _p2p.MessengerServers;
        if (sync == null)
            return;
        try
        {
            await sync.DrainInboxOnceAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Drain inbox on chat open");
        }
    }

    private async Task StartChatSessionIfNeededAsync(ChatEntity chat, ChatP2PSession session)
    {
        if (_p2p.IsChatSessionStarted(chat.Id))
            return;

        try
        {
            await session.StartAsync().ConfigureAwait(false);
            _p2p.MarkChatSessionStarted(chat.Id);
            AppLog.PeerConnected("chat-session", chat.PeerNetworkIdShort);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not start UDP for chat {ChatId}", chat.Id);
            if (_chat == null || _chat.Id != chat.Id)
                return;
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert(Loc.T("error"), Loc.Tf("chat.udp_fail", ex.Message), Loc.T("ok"))
                    .ConfigureAwait(true);
            }).ConfigureAwait(false);
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_chat != null)
            ActiveChatTracker.Clear(_chat.Id);
        _ = StopVoiceRecordingAndDiscardAsync();
        VoiceMessagePlayer.Stop();
        _p2p.LocalScan.ClientsChanged -= OnPeerLanPresenceChanged;
        _repo.PeerPublicKeyChanged -= OnPeerPublicKeyChanged;
        _repo.ChatMessageAppended -= OnChatMessageAppended;
        _messengerServers.FailoverCompleted -= OnMessengerServerFailover;
        if (_presenceRefreshTimer != null)
            _presenceRefreshTimer.Stop();
        _peerNetworkIdShort = null;
        _chat = null;
        Interlocked.Increment(ref _reloadEpoch);
        if (_p2pSession != null)
        {
            _p2pSession.MessagesChanged -= OnP2PMessagesChanged;
            _p2pSession.TransferStateChanged -= OnP2PTransferStateChanged;
            _p2pSession = null;
        }
    }

    private void OnPeerLanPresenceChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(RefreshPeerPresenceLabel);
    }

    private void RefreshSafetyLabel(ChatEntity chat)
    {
        SafetyNumberLabel.Text = PeerSafetyUi.ChatPanel(_auth, chat);
    }

    private static void SetControlHint(View view, string text)
    {
        ToolTipProperties.SetText(view, text);
        SemanticProperties.SetHint(view, text);
        SemanticProperties.SetDescription(view, text);
    }

    private void OnPeerPublicKeyChanged(object? sender, PeerPublicKeyChangedEventArgs e)
    {
        if (e.ChatId != ChatId)
            return;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var fresh = await _repo.GetChatAsync(ChatId).ConfigureAwait(true);
            if (fresh != null)
            {
                _chat = fresh;
                Title = fresh.PeerNickname;
                PeerNameLabel.Text = fresh.PeerNickname;
                RefreshSafetyLabel(fresh);
            }

            await DisplayAlert(
                Loc.T("safety.key_change_title"),
                Loc.Tf("safety.key_change_body", e.PeerNickname, e.PreviousSafetyNumber, e.NewSafetyNumber),
                Loc.T("ok")).ConfigureAwait(true);
        });
    }

    private void OnMessengerServerFailover(object? sender, MessengerServerFailoverEventArgs e)
    {
        if (!e.SwitchedToMesh)
            return;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await DisplayAlert(Loc.T("safety.mesh_title"), Loc.T("safety.mesh_body"), Loc.T("ok"))
                .ConfigureAwait(true);
        });
    }

    private async void OnEmergencyUntrustClicked(object? sender, EventArgs e)
    {
        await PeerSafetyUi.MarkUntrustedAsync(_messengerServers, _chat, _logger, this).ConfigureAwait(true);
    }

    private void OnP2PMessagesChanged(object? sender, EventArgs e)
    {
        if (_blacklist.IsBlocked(_auth.CurrentUser?.Id, _peerNetworkIdShort))
            return;
        ScheduleReloadMessages();
    }

    private void OnChatMessageAppended(object? sender, ChatMessageAppendedEventArgs e)
    {
        if (e.ChatId != ChatId)
            return;
        if (_blacklist.IsBlocked(_auth.CurrentUser?.Id, _peerNetworkIdShort))
            return;
        ScheduleReloadMessages();
    }

    private void OnP2PTransferStateChanged(object? sender, int messageId)
    {
        ScheduleReloadMessages();
    }

    private void ScheduleReloadMessages()
    {
        var epoch = Interlocked.Increment(ref _reloadEpoch);
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(200).ConfigureAwait(true);
            if (epoch != Volatile.Read(ref _reloadEpoch))
                return;
            await ReloadMessagesAsync().ConfigureAwait(true);
        });
    }

    private void EnsurePresenceRefreshTimerStarted()
    {
        _presenceRefreshTimer ??= Dispatcher.CreateTimer();
        _presenceRefreshTimer.Interval = TimeSpan.FromSeconds(2);
        _presenceRefreshTimer.Tick -= OnPresenceRefreshTimerTick;
        _presenceRefreshTimer.Tick += OnPresenceRefreshTimerTick;
        if (!_presenceRefreshTimer.IsRunning)
            _presenceRefreshTimer.Start();
    }

    private void OnPresenceRefreshTimerTick(object? sender, EventArgs e)
    {
        RefreshPeerPresenceLabel();
    }

    private async Task ReloadMessagesAsync()
    {
        if (_blacklist.IsBlocked(_auth.CurrentUser?.Id, _peerNetworkIdShort ?? _chat?.PeerNetworkIdShort))
        {
            _messageItems.Clear();
            _loadedRows.Clear();
            _attachmentDurationLabels.Clear();
            return;
        }

        if (_isLoadingRows)
        {
            _pendingReload = true;
            return;
        }

        _isLoadingRows = true;
        _suppressLoadMore = true;
        try
        {
            var take = Math.Max(MessagesPageSize, _loadedRows.Count);
            // DB page is newest-first; display ascending (oldest top, newest bottom).
            var pageDesc = await _repo.ListMessagesPageDescAsync(ChatId, 0, take).ConfigureAwait(true);
            _attachmentDurationLabels.Clear();
            CaptureDurationsAndReleasePayloadBlobs(pageDesc);
            _hasMoreRows = pageDesc.Count == take;
            _loadedRows.Clear();
            _loadedRows.AddRange(pageDesc);
            var chronological = pageDesc.Reverse().ToList();
            SyncMessageItems(chronological);
            ScrollMessagesToEnd();
        }
        finally
        {
            _isLoadingRows = false;
            if (_pendingReload)
            {
                _pendingReload = false;
                await ReloadMessagesAsync().ConfigureAwait(true);
            }
        }
    }

    private async Task LoadNextMessagesPageAsync()
    {
        if (_isLoadingRows || !_hasMoreRows)
            return;
        _isLoadingRows = true;
        try
        {
            var pageDesc = await _repo.ListMessagesPageDescAsync(ChatId, _loadedRows.Count, MessagesPageSize)
                .ConfigureAwait(true);
            CaptureDurationsAndReleasePayloadBlobs(pageDesc);
            _hasMoreRows = pageDesc.Count == MessagesPageSize;
            _loadedRows.AddRange(pageDesc);
            // Older page (DESC) → chronological, prepend so newest stay at bottom.
            var chronologicalOlder = pageDesc.Reverse().ToList();
            for (var i = 0; i < chronologicalOlder.Count; i++)
                _messageItems.Insert(i, BuildMessageRowVm(chronologicalOlder[i]));
        }
        finally
        {
            _isLoadingRows = false;
        }
    }

    private void ScrollMessagesToEnd()
    {
        if (_messageItems.Count == 0)
            return;

        // CollectionView often ignores ScrollTo until items are measured; defer + retry.
        var epoch = Interlocked.Increment(ref _scrollToEndEpoch);
        Dispatcher.Dispatch(async () =>
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                await Task.Delay(attempt == 0 ? 16 : 48).ConfigureAwait(true);
                if (epoch != Volatile.Read(ref _scrollToEndEpoch) || _messageItems.Count == 0)
                    return;
                try
                {
                    MessagesCollection.ScrollTo(_messageItems[^1], position: ScrollToPosition.End, animate: false);
                }
                catch
                {
                    // CollectionView may not be ready yet.
                }
            }
        });
    }

    private void SyncMessageItems(IReadOnlyList<ChatMessageEntity> page)
    {
        while (_messageItems.Count > page.Count)
            _messageItems.RemoveAt(_messageItems.Count - 1);

        for (var i = 0; i < page.Count; i++)
        {
            var previous = i < _messageItems.Count ? _messageItems[i] : null;
            var next = BuildMessageRowVm(page[i]);
            if (previous == null)
                _messageItems.Add(next);
            else if (!MessageRowsEqual(previous, next))
                _messageItems[i] = next;
        }
    }

    private static bool MessageRowsEqual(MessageRowVm a, MessageRowVm b) =>
        a.MessageId == b.MessageId &&
        a.DeliveryStatus == b.DeliveryStatus &&
        a.TextBody == b.TextBody &&
        a.FileBodyText == b.FileBodyText &&
        a.IsImage == b.IsImage &&
        a.IsFile == b.IsFile &&
        a.IsVoice == b.IsVoice &&
        a.VoiceReady == b.VoiceReady &&
        a.IsTransferOffer == b.IsTransferOffer &&
        a.ShowDelivery == b.ShowDelivery &&
        a.DeliveryGlyph == b.DeliveryGlyph &&
        a.TimeLabel == b.TimeLabel;

    /// <summary>
    /// После выборки страницы сразу отпускаем BLOB: в списке они не нужны,
    /// открытие идёт через <see cref="OpenOrDownloadAttachmentAsync"/>.
    /// Длительность голоса/видео снимаем до освобождения байт.
    /// </summary>
    private void CaptureDurationsAndReleasePayloadBlobs(IEnumerable<ChatMessageEntity> rows)
    {
        foreach (var m in rows)
        {
            if (m.ImageBlob is not { Length: > 0 } blob)
                continue;
            if (m.TransferSizeBytes <= 0)
                m.TransferSizeBytes = blob.Length;

            if (IsVoiceAttachment(m) || IsVideoAttachment(m))
            {
                var duration = MediaDuration.TryGet(blob, m.MimeType,
                    m.TransferFileName.Length > 0 ? m.TransferFileName : m.Text);
                if (duration != null)
                    _attachmentDurationLabels[m.Id] = MediaDuration.Format(duration.Value);
                else
                    _attachmentDurationLabels.Remove(m.Id);
            }

            m.ImageBlob = null;
        }
    }

    private MessageRowVm BuildMessageRowVm(ChatMessageEntity m)
    {
        var color = m.Outgoing ? IskraTheme.SentText : IskraTheme.Text;
        var sentLocal = new DateTimeOffset(m.SentUtcTicks, TimeSpan.Zero).ToLocalTime();
        var ts = sentLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
        var ds = (MessageDeliveryStatus)m.DeliveryStatus;
        if (m.Outgoing && ds == MessageDeliveryStatus.NotApplicable)
            ds = MessageDeliveryStatus.Delivered;
        var (glyph, gColor, show) = DeliveryUiFor(ds, m.Outgoing);
        var bubble = m.Outgoing ? IskraTheme.OutgoingBubble : IskraTheme.IncomingBubble;

        if (m.PayloadKind == (int)ChatPayloadKind.File)
            return AttachmentPlaceholder(m, isTransferOffer: false, ds, color, show, glyph, gColor, bubble, ts);

        if (m.PayloadKind == (int)ChatPayloadKind.Image)
            return AttachmentPlaceholder(m, isTransferOffer: false, ds, color, show, glyph, gColor, bubble, ts);

        if (m.PayloadKind == (int)ChatPayloadKind.TransferOffer)
        {
            var state = (ChatTransferState)m.TransferState;
            var localReady = m.Outgoing || state == ChatTransferState.Received;
            if (localReady)
                return AttachmentPlaceholder(m, isTransferOffer: false, ds, color, show, glyph, gColor, bubble, ts);

            var stateText = state switch
            {
                ChatTransferState.Transferring => Loc.T("chat.state.loading"),
                ChatTransferState.Failed => Loc.T("chat.state.failed"),
                _ => Loc.T("chat.state.tap_download")
            };
            return AttachmentPlaceholder(m, isTransferOffer: true, ds, color, show, glyph, gColor, bubble, ts, stateText);
        }

        return new MessageRowVm
        {
            CaptionLine = "",
            TextBody = m.Text,
            ShowTextBody = true,
            IsImage = false,
            IsFile = false,
            IsTransferOffer = false,
            IsVoice = false,
            VoiceReady = false,
            MessageId = m.Id,
            MessageColor = color,
            ShowDelivery = show,
            DeliveryGlyph = glyph,
            DeliveryGlyphColor = gColor,
            Outgoing = m.Outgoing,
            BubbleColumn = m.Outgoing ? 0 : 2,
            DeliveryStatus = ds,
            BubbleColor = bubble,
            TimeLabel = ts
        };
    }

    private MessageRowVm AttachmentPlaceholder(
        ChatMessageEntity m,
        bool isTransferOffer,
        MessageDeliveryStatus deliveryStatus,
        Color color,
        bool show,
        string glyph,
        Color gColor,
        Color bubble,
        string ts,
        string? stateText = null)
    {
        var kb = (AttachmentSizeBytes(m) + 1023) / 1024;
        var name = AttachmentDisplayName(m);
        var isVoice = IsVoiceAttachment(m);
        var voiceReady = isVoice && IsVoiceLocallyAvailable(m, isTransferOffer);
        var duration = TryFormatLocalMediaDuration(m);
        var nameWithDuration = duration == null ? name : $"{name} · {duration}";
        string fileBody;
        if (isVoice)
        {
            var icon = voiceReady ? "▶️" : "⬇️";
            var hint = voiceReady
                ? Loc.T("chat.state.tap_play")
                : (stateText ?? Loc.T("chat.state.tap_download"));
            fileBody = $"{icon} {nameWithDuration} · {Loc.Tf("chat.kb", kb)} · {hint}";
        }
        else
        {
            var action = stateText ?? Loc.T("chat.state.tap_row");
            fileBody = $"{nameWithDuration} · {Loc.Tf("chat.kb", kb)} · {action}";
        }

        return new MessageRowVm
        {
            CaptionLine = AttachmentKindCaption(m),
            TextBody = "",
            FileBodyText = fileBody,
            ShowTextBody = false,
            IsImage = IsImageAttachment(m),
            IsFile = true,
            IsTransferOffer = isTransferOffer,
            IsVoice = isVoice,
            VoiceReady = voiceReady,
            MessageId = m.Id,
            MessageColor = color,
            ShowDelivery = show,
            DeliveryGlyph = glyph,
            DeliveryGlyphColor = gColor,
            Outgoing = m.Outgoing,
            BubbleColumn = m.Outgoing ? 0 : 2,
            DeliveryStatus = deliveryStatus,
            BubbleColor = bubble,
            TimeLabel = ts
        };
    }

    private static bool IsVoiceLocallyAvailable(ChatMessageEntity m, bool isTransferOffer)
    {
        if (m.Outgoing)
            return true;
        if (m.ImageBlob is { Length: > 0 })
            return true;
        if ((ChatTransferState)m.TransferState == ChatTransferState.Received)
            return true;
        if (!isTransferOffer && m.PayloadKind is (int)ChatPayloadKind.File or (int)ChatPayloadKind.Image)
            return true;
        return false;
    }

    private static int AttachmentSizeBytes(ChatMessageEntity m) =>
        (int)(m.ImageBlob is { Length: > 0 } blob ? blob.Length : m.TransferSizeBytes);

    /// <summary>Duration when local bytes were present at list load (outgoing or downloaded).</summary>
    private string? TryFormatLocalMediaDuration(ChatMessageEntity m)
    {
        if (_attachmentDurationLabels.TryGetValue(m.Id, out var cached))
            return cached;
        if (m.ImageBlob is not { Length: > 0 } blob)
            return null;
        if (!IsVoiceAttachment(m) && !IsVideoAttachment(m))
            return null;

        var duration = MediaDuration.TryGet(blob, m.MimeType,
            m.TransferFileName.Length > 0 ? m.TransferFileName : m.Text);
        if (duration == null)
            return null;
        var label = MediaDuration.Format(duration.Value);
        _attachmentDurationLabels[m.Id] = label;
        return label;
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
        return "file";
    }

    private static string AttachmentKindCaption(ChatMessageEntity m)
    {
        if (IsVoiceAttachment(m))
            return Loc.T("chat.caption.voice");
        if (IsImageAttachment(m))
            return Loc.T("chat.caption.image");
        if (IsVideoAttachment(m))
            return Loc.T("chat.caption.video");
        return Loc.T("chat.caption.file");
    }

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

    private void OnMessagesScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (Math.Abs(e.VerticalDelta) > 0.5)
            _suppressLoadMore = false;

        // Chronological list: load older messages when the user scrolls near the top.
        if (!_suppressLoadMore && e.FirstVisibleItemIndex <= 1 && e.VerticalDelta < 0)
            _ = LoadNextMessagesPageAsync();
    }

    private async void OnMessagesRemainingItemsThresholdReached(object? sender, EventArgs e)
    {
        // Kept for CollectionView; primary load-more is top-scroll in OnMessagesScrolled.
        await Task.CompletedTask.ConfigureAwait(true);
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var text = MessageEntry.Text?.Trim() ?? "";
        if (text.Length == 0 || _p2pSession == null)
            return;
        ClearDeliveryIssue();
        MessageEntry.Text = string.Empty;

        try
        {
            await _p2pSession.SendTextAsync(text).ConfigureAwait(true);
            ClearDeliveryIssue();
        }
        catch (OutboundMessageQueuedException ex)
        {
            _logger.LogInformation(ex, "Message queued until peer is on LAN");
            ShowDeliveryIssue(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send message failed");
            MessageEntry.Text = text;
            ShowDeliveryIssue(ex.Message);
        }
        finally
        {
            await ReloadMessagesAsync().ConfigureAwait(true);
        }
    }

    private async void OnVoiceClicked(object? sender, EventArgs e)
    {
        if (_p2pSession == null)
            return;

        if (_voice is { IsRecording: true })
        {
            await StopVoiceRecordingAndSendAsync().ConfigureAwait(true);
            return;
        }

        await StartVoiceRecordingAsync().ConfigureAwait(true);
    }

    private async Task SyncTrafficQualityAsync()
    {
        var persisted = await _routingStore.LoadAsync().ConfigureAwait(false);
        MediaEconomy.Apply(_p2p, persisted.TrafficQuality);
    }

    private async Task StartVoiceRecordingAsync()
    {
        ClearDeliveryIssue();
        var mic = await Permissions.RequestAsync<Permissions.Microphone>().ConfigureAwait(true);
        if (mic != PermissionStatus.Granted)
        {
            ShowDeliveryIssue(Loc.T("chat.mic_denied"));
            return;
        }

        try
        {
            await SyncTrafficQualityAsync().ConfigureAwait(true);
            _voice = new VoiceRecordingSession();
            await _voice.StartAsync(MediaEconomy.SpeechBitrate(_p2p)).ConfigureAwait(true);
            VoiceButton.Text = "■";
            VoiceButton.TextColor = Colors.Red;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Voice recording start failed");
            ShowDeliveryIssue(ex.Message);
            await StopVoiceRecordingAndDiscardAsync().ConfigureAwait(true);
        }
    }

    private async Task StopVoiceRecordingAndSendAsync()
    {
        ResetVoiceButton();
        var voice = _voice;
        _voice = null;
        if (voice == null)
        {
            ShowDeliveryIssue(Loc.T("chat.voice_file_fail"));
            return;
        }

        try
        {
            await voice.StopCaptureAsync().ConfigureAwait(true);
            await SyncTrafficQualityAsync().ConfigureAwait(true);
            if (_p2pSession == null)
            {
                await voice.DiscardAsync().ConfigureAwait(true);
                return;
            }

            QueueBinarySend((session, ct) => FinishAndSendVoiceAsync(session, voice, ct));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stop voice recording failed");
            ShowDeliveryIssue(ex.Message);
            try
            {
                await voice.DiscardAsync().ConfigureAwait(true);
            }
            catch
            {
                // ignore
            }
        }
    }

    private async Task StopVoiceRecordingAndDiscardAsync()
    {
        try
        {
            if (_voice != null)
                await _voice.DiscardAsync().ConfigureAwait(true);
        }
        catch
        {
            // ignore
        }
        finally
        {
            _voice = null;
            ResetVoiceButton();
        }
    }

    private void ResetVoiceButton()
    {
        VoiceButton.Text = "🎤";
        VoiceButton.TextColor = IskraTheme.Text;
    }

    private async void OnMessageRowTapped(object? sender, TappedEventArgs e)
    {
        var walk = sender switch
        {
            TapGestureRecognizer tg => tg.Parent as Element,
            Element el => el,
            _ => null
        };
        MessageRowVm? vm = null;
        for (var el = walk; el != null; el = el.Parent as Element)
            if (el.BindingContext is MessageRowVm row)
            {
                vm = row;
                break;
            }

        if (vm == null || vm.MessageId == 0)
            return;

        if (vm.IsRetryable)
        {
            await RetryFailedMessageAsync(vm.MessageId).ConfigureAwait(true);
            return;
        }

        if (!vm.IsFile && !vm.IsImage)
            return;

        _ = OpenOrDownloadAttachmentAsync(vm.MessageId);
    }

    private async Task OpenOrDownloadAttachmentAsync(int messageId)
    {
        try
        {
            var row = await _repo.GetMessageAsync(messageId).ConfigureAwait(true);
            if (row == null)
            {
                await DisplayAlert(Loc.T("chat.file"), Loc.T("chat.msg_missing"), Loc.T("ok")).ConfigureAwait(true);
                return;
            }

            if (IsVoiceAttachment(row))
            {
                if (row.ImageBlob is { Length: > 0 })
                {
                    await PlayVoiceAttachmentAsync(row.ImageBlob).ConfigureAwait(true);
                    return;
                }

                var canDownloadVoice = _p2pSession != null &&
                                       !row.Outgoing &&
                                       !string.IsNullOrWhiteSpace(row.TransferId);
                if (!canDownloadVoice)
                {
                    await DisplayAlert(Loc.T("chat.voice"), Loc.T("chat.voice_not_ready"), Loc.T("ok"))
                        .ConfigureAwait(true);
                    return;
                }

                QueueBinaryDownload(messageId);
                return;
            }

            if (row.ImageBlob is { Length: > 0 })
            {
                await DisplayAttachmentAsync(row).ConfigureAwait(true);
                return;
            }

            var canDownloadFile = _p2pSession != null &&
                                  !row.Outgoing &&
                                  !string.IsNullOrWhiteSpace(row.TransferId);
            if (!canDownloadFile)
            {
                await DisplayAlert(Loc.T("chat.file"), Loc.T("chat.msg_missing"), Loc.T("ok")).ConfigureAwait(true);
                return;
            }

            QueueBinaryDownload(messageId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open attachment failed");
            await DisplayAlert(Loc.T("chat.file"), ex.Message, Loc.T("ok")).ConfigureAwait(true);
        }
    }

    private void QueueBinaryDownload(int messageId)
    {
        var session = _p2pSession;
        if (session == null)
            return;
        if (!_binaryDownloadsInFlight.TryAdd(messageId, 0))
            return;
        ClearDeliveryIssue();
        _ = RunBinaryDownloadAsync(session, messageId);
    }

    private async Task RunBinaryDownloadAsync(ChatP2PSession session, int messageId)
    {
        try
        {
            await session.RequestBinaryDownloadAsync(messageId).ConfigureAwait(false);
            if (_chat == null || _p2pSession == null)
                return;

            var row = await _repo.GetMessageAsync(messageId).ConfigureAwait(false);
            if (row?.ImageBlob is not { Length: > 0 })
                return;

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (_chat == null)
                    return;
                if (IsVoiceAttachment(row))
                    await PlayVoiceAttachmentAsync(row.ImageBlob).ConfigureAwait(true);
                else
                    await DisplayAttachmentAsync(row).ConfigureAwait(true);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Transfer download failed in chat {ChatId}", ChatId);
            MainThread.BeginInvokeOnMainThread(() => ShowDeliveryIssue(ex.Message));
        }
        finally
        {
            _binaryDownloadsInFlight.TryRemove(messageId, out _);
        }
    }

    private async Task PlayVoiceAttachmentAsync(byte[] oggBytes)
    {
        try
        {
            AppLog.BinaryLoaded("play-voice", "voice.ogg", oggBytes.Length);
            await VoiceMessagePlayer.PlayAsync(oggBytes, _logger).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Voice play failed");
            await DisplayAlert(Loc.T("chat.playback"), ex.Message, Loc.T("ok")).ConfigureAwait(true);
        }
    }

    private async Task DisplayAttachmentAsync(ChatMessageEntity row)
    {
        if (row.ImageBlob is not { Length: > 0 } blob)
        {
            await DisplayAlert(Loc.T("chat.file"), Loc.T("chat.msg_missing"), Loc.T("ok")).ConfigureAwait(true);
            return;
        }

        if (IsVoiceAttachment(row))
        {
            await PlayVoiceAttachmentAsync(blob).ConfigureAwait(true);
            return;
        }

        var isImage = IsImageAttachment(row);
        var isVideo = IsVideoAttachment(row);
        var name = SanitizeFileName(EnsureMediaFileName(AttachmentDisplayName(row), row));
        var temp = Path.Combine(FileSystem.CacheDirectory, $"{row.Id}_{name}");
        await File.WriteAllBytesAsync(temp, blob).ConfigureAwait(true);
        AppLog.BinaryLoaded(isImage ? "received-image" : isVideo ? "received-video" : "received-document", name,
            blob.Length);
        if (isImage)
        {
            await Navigation.PushModalAsync(new NavigationPage(new ImagePreviewPage(temp))
            {
                BarBackgroundColor = Colors.Black,
                BarTextColor = Colors.White
            }).ConfigureAwait(true);
            return;
        }

        if (isVideo)
        {
            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                Title = Loc.T("chat.video"),
                File = new ReadOnlyFile(temp)
            }).ConfigureAwait(true);
            return;
        }

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = Loc.T("chat.save_doc"),
            File = new ShareFile(temp)
        }).ConfigureAwait(true);
    }

    private async Task TryRefreshPeerNicknameDisplayAsync(ChatEntity chat)
    {
        var id = chat.PeerNetworkIdShort.Trim();
        var display = ResolvePeerDisplayName(chat);
        if (!string.Equals(display, chat.PeerNickname, StringComparison.Ordinal))
        {
            await _repo.TryUpdatePeerNicknameAsync(chat.Id, display).ConfigureAwait(true);
            chat.PeerNickname = display;
        }

        Title = display;
        PeerNameLabel.Text = display;
        PeerAvatarInitials.Text = IskraTheme.Initials(display);
        PeerIdLabel.Text = Loc.Tf("chat.node", id);
        RefreshSafetyLabel(chat);
    }

    private string ResolvePeerDisplayName(ChatEntity chat)
    {
        var id = chat.PeerNetworkIdShort.Trim();
        var nick = chat.PeerNickname?.Trim() ?? "";
        if (!ChatRepository.IsPlaceholderNickname(nick, id))
            return nick;

        foreach (var p in _p2p.LocalScan.Clients)
        {
            if (!string.Equals(p.NetworkId.ToShortString(), id, StringComparison.Ordinal))
                continue;
            var discovered = p.Nickname?.Trim() ?? "";
            if (!ChatRepository.IsPlaceholderNickname(discovered, id))
                return discovered;
        }

        return nick.Length > 0 ? nick : id;
    }

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
        return name;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (invalid.Contains(chars[i]))
                chars[i] = '_';

        var s = new string(chars).Trim();
        return string.IsNullOrEmpty(s) ? "document" : s;
    }

    private static (string Glyph, Color GlyphColor, bool Show) DeliveryUiFor(MessageDeliveryStatus status,
        bool outgoing)
    {
        if (!outgoing)
            return ("", Colors.Transparent, false);
        return status switch
        {
            MessageDeliveryStatus.Pending => (OutgoingDeliveryIndicators.Pending, Color.FromArgb("#B8860B"), true),
            MessageDeliveryStatus.Delivered => ("\u2713\u2713", IskraTheme.Check, true),
            MessageDeliveryStatus.Failed => (OutgoingDeliveryIndicators.Failed, Colors.Red, true),
            _ => ("\u2713\u2713", IskraTheme.Check, true)
        };
    }

    private static bool TryGetDocumentOrVideoMime(string fileName, out string mime)
    {
        if (DocumentAttachHelper.TryGetMimeFromExtension(fileName, out mime))
            return true;
        mime = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".wmv" => "video/x-ms-wmv",
            ".webm" => "video/webm",
            ".ogv" => "video/ogg",
            _ => ""
        };
        return mime.Length > 0;
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync().ConfigureAwait(true);
    }

    private static bool IsAppxManifestMissing(FileNotFoundException ex) =>
        ex.FileName?.Contains("AppxManifest.xml", StringComparison.OrdinalIgnoreCase) == true ||
        ex.Message.Contains("AppxManifest.xml", StringComparison.OrdinalIgnoreCase);

#if ANDROID
    private static async Task EnsureLegacyStorageWriteAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.StorageWrite>().ConfigureAwait(true);
            if (status != PermissionStatus.Granted)
                await Permissions.RequestAsync<Permissions.StorageWrite>().ConfigureAwait(true);
        }
        catch
        {
            // StorageWrite may be no-op / unavailable on newer APIs — capture can still use app cache.
        }
    }
#endif

    private static Color GetPaletteColor(string key)
    {
        var hash = Math.Abs(key.GetHashCode(StringComparison.Ordinal));
        const int hueSteps = 12;
        const int lightSteps = 3;
        var h = hash % hueSteps;
        var lBand = hash / hueSteps % lightSteps;
        var hueDeg = h * (360.0f / hueSteps);
        var lightness = 0.40f + lBand * 0.06f;
        return Color.FromHsla(hueDeg / 360.0f, 0.72f, lightness);
    }

    private void RefreshPeerPresenceLabel()
    {
        if (string.IsNullOrWhiteSpace(_peerNetworkIdShort))
            return;

        var online = _p2p.LocalScan.IsPeerSeenRecentlyOnLan(_peerNetworkIdShort);
        PeerPresenceDot.Fill = online ? IskraTheme.Online : IskraTheme.Danger;
        PeerStatusLabel.Text = online ? Loc.T("online") : Loc.T("offline");
        PeerStatusLabel.TextColor = online ? IskraTheme.Online : IskraTheme.Muted;
    }

    private void ShowDeliveryIssue(string message)
    {
        DeliveryIssueLabel.Text = string.IsNullOrWhiteSpace(message)
            ? Loc.T("chat.delivery_issue")
            : message.Trim();
        DeliveryIssueLabel.IsVisible = true;
    }

    private void ClearDeliveryIssue()
    {
        DeliveryIssueLabel.Text = string.Empty;
        DeliveryIssueLabel.IsVisible = false;
    }

    private async void OnBlockPeerClicked(object? sender, EventArgs e)
    {
        var user = _auth.CurrentUser;
        var chat = _chat;
        if (user == null || chat == null)
            return;

        var blocked = await BlacklistUi.ConfirmAndBlockAsync(
            this, _blacklist, user.Id, chat.PeerNetworkIdShort, chat.PeerNickname).ConfigureAwait(true);
        if (blocked && Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync().ConfigureAwait(true);
    }

    private async void OnClearChatClicked(object? sender, EventArgs e)
    {
        if (_p2pSession == null)
            return;

        var confirm = await DisplayAlert(
            Loc.T("chat.clear_title"),
            Loc.T("chat.clear_body"),
            Loc.T("delete"),
            Loc.T("cancel")).ConfigureAwait(true);
        if (!confirm)
            return;

        ClearDeliveryIssue();
        try
        {
            var ok = await _p2pSession.ClearMessagesAsync().ConfigureAwait(true);
            if (!ok)
                await DisplayAlert(Loc.T("error"), Loc.T("chat.clear_fail"), Loc.T("ok")).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Clear chat failed for chat {ChatId}", ChatId);
            await DisplayAlert(Loc.T("error"), ex.Message, Loc.T("ok")).ConfigureAwait(true);
        }
        finally
        {
            await ReloadMessagesAsync().ConfigureAwait(true);
        }
    }

    private async Task RetryFailedMessageAsync(int messageId)
    {
        if (_p2pSession == null)
            return;

        ClearDeliveryIssue();
        try
        {
            await _p2pSession.RetryFailedMessageAsync(messageId).ConfigureAwait(true);
            ClearDeliveryIssue();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Retry failed message failed");
            ShowDeliveryIssue(ex.Message);
        }
        finally
        {
            await ReloadMessagesAsync().ConfigureAwait(true);
        }
    }
}