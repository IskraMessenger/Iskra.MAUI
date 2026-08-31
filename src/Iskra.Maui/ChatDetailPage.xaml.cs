using System.Collections.ObjectModel;
using System.Globalization;
using Iskra.Maui.Localization;
using Iskra.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
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
        RefreshSafetyLabel(chat);
        await TryRefreshPeerNicknameDisplayAsync(chat).ConfigureAwait(true);
        if (user == null)
        {
            _peerNetworkIdShort = null;
            _chat = null;
            await Navigation.PopAsync().ConfigureAwait(true);
            return;
        }

        try
        {
            await _p2p.EnsureStartedAsync(user).ConfigureAwait(true);
            await MessengerServersBootstrap.EnsureRunningAsync(_p2p, _logger).ConfigureAwait(true);
            await MessengerServersBootstrap.PublishChatRequestAsync(_p2p, chat.PeerNetworkIdShort, _logger)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ensure P2P (invite listener) on chat detail");
        }

        _p2p.LocalScan.ClientsChanged += OnPeerLanPresenceChanged;
        _repo.PeerPublicKeyChanged += OnPeerPublicKeyChanged;
        _messengerServers.FailoverCompleted += OnMessengerServerFailover;
        EnsurePresenceRefreshTimerStarted();
        var uiSync = SynchronizationContext.Current;
        _p2pSession = _p2p.GetSession(chat, user, _auth, _repo, uiSync);
        _p2pSession.MessagesChanged += OnP2PMessagesChanged;
        _p2pSession.TransferStateChanged += OnP2PTransferStateChanged;
        if (!_p2p.IsChatSessionStarted(chat.Id))
            try
            {
                await _p2pSession.StartAsync().ConfigureAwait(true);
                _p2p.MarkChatSessionStarted(chat.Id);
                AppLog.PeerConnected("chat-session", chat.PeerNetworkIdShort);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not start UDP for chat {ChatId}", chat.Id);
                await DisplayAlert(Loc.T("error"), Loc.Tf("chat.udp_fail", ex.Message), Loc.T("ok")).ConfigureAwait(true);
            }

        await ReloadMessagesAsync().ConfigureAwait(true);
        RefreshPeerPresenceLabel();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _ = StopVoiceRecordingAndDiscardAsync();
        VoiceMessagePlayer.Stop();
        _p2p.LocalScan.ClientsChanged -= OnPeerLanPresenceChanged;
        _repo.PeerPublicKeyChanged -= OnPeerPublicKeyChanged;
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
            ReleaseListPayloadBlobs(pageDesc);
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
            ReleaseListPayloadBlobs(pageDesc);
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
    /// </summary>
    private static void ReleaseListPayloadBlobs(IEnumerable<ChatMessageEntity> rows)
    {
        foreach (var m in rows)
        {
            if (m.ImageBlob is not { Length: > 0 } blob)
                continue;
            if (m.TransferSizeBytes <= 0)
                m.TransferSizeBytes = blob.Length;
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

    private static MessageRowVm AttachmentPlaceholder(
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
        string fileBody;
        if (isVoice)
        {
            var icon = voiceReady ? "▶️" : "⬇️";
            var hint = voiceReady
                ? Loc.T("chat.state.tap_play")
                : (stateText ?? Loc.T("chat.state.tap_download"));
            fileBody = $"{icon} {name} · {Loc.Tf("chat.kb", kb)} · {hint}";
        }
        else
        {
            var action = stateText ?? Loc.T("chat.state.tap_row");
            fileBody = $"{name} · {Loc.Tf("chat.kb", kb)} · {action}";
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

        try
        {
            await MessengerServersBootstrap.EnsureRunningAsync(_p2p, _logger).ConfigureAwait(true);
            await _p2pSession.SendTextAsync(text).ConfigureAwait(true);
            MessageEntry.Text = string.Empty;
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
        var persisted = await _routingStore.LoadAsync().ConfigureAwait(true);
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
        try
        {
            if (_voice == null)
            {
                ShowDeliveryIssue(Loc.T("chat.voice_file_fail"));
                return;
            }

            var recorded = await _voice.StopAsync().ConfigureAwait(true);
            _voice = null;
            AppLog.BinaryLoaded("voice", recorded.FileName, recorded.Bytes.Length);
            _media.ValidateDocumentMime(recorded.MimeType);
            _media.ValidateDocumentSize(recorded.Bytes.Length);
            await PrepareBinarySendAsync().ConfigureAwait(true);
            await _p2pSession!.SendFileAsync(recorded.FileName, recorded.Bytes, recorded.MimeType).ConfigureAwait(true);
            ClearDeliveryIssue();
        }
        catch (OutboundMessageQueuedException ex)
        {
            _logger.LogInformation(ex, "Voice message queued until peer is on LAN");
            ShowDeliveryIssue(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send voice message failed");
            ShowDeliveryIssue(ex.Message);
            await StopVoiceRecordingAndDiscardAsync().ConfigureAwait(true);
        }
        finally
        {
            await ReloadMessagesAsync().ConfigureAwait(true);
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

    private async void OnAttachImageClicked(object? sender, EventArgs e)
    {
        if (_p2pSession == null)
            return;
        ClearDeliveryIssue();

        try
        {
            var pick = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = Loc.T("chat.image_picker"),
                FileTypes = FilePickerFileType.Images
            }).ConfigureAwait(true);
            if (pick == null)
                return;

            if (!ImageAttachHelper.TryGetMimeFromExtension(pick.FileName, out var mime))
            {
                await DisplayAlert(Loc.T("chat.file"), Loc.T("chat.only_images"), Loc.T("ok")).ConfigureAwait(true);
                return;
            }

            await using var stream = await pick.OpenReadAsync().ConfigureAwait(true);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(true);
            var bytes = ms.ToArray();
            AppLog.BinaryLoaded("image", pick.FileName, bytes.Length);
            if (bytes.Length < 12)
            {
                await DisplayAlert(Loc.T("chat.file"), Loc.T("chat.file_too_small"), Loc.T("ok")).ConfigureAwait(true);
                return;
            }

            if (!ImageAttachHelper.SniffMatchesMime(bytes.AsSpan(0, Math.Min(12, bytes.Length)), mime))
            {
                await DisplayAlert(Loc.T("chat.file"), Loc.T("chat.file_mismatch"), Loc.T("ok")).ConfigureAwait(true);
                return;
            }

            var fitted = await TryFitOutgoingImageAsync(bytes, mime, askIfOverDefault: true).ConfigureAwait(true);
            if (fitted == null)
                return;
            bytes = fitted.Value.Bytes;
            mime = fitted.Value.Mime;

            _media.ValidateMime(mime);
            await PrepareBinarySendAsync().ConfigureAwait(true);
            await _p2pSession.SendImageAsync(bytes, mime).ConfigureAwait(true);
            ClearDeliveryIssue();
        }
        catch (OutboundMessageQueuedException ex)
        {
            _logger.LogInformation(ex, "Image queued until peer is on LAN");
            ShowDeliveryIssue(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send image failed");
            ShowDeliveryIssue(ex.Message);
        }
        finally
        {
            await ReloadMessagesAsync().ConfigureAwait(true);
        }
    }

    private async void OnAttachDocumentClicked(object? sender, EventArgs e)
    {
        if (_p2pSession == null)
            return;
        ClearDeliveryIssue();

        try
        {
            var pick = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = Loc.T("chat.doc_picker"),
                FileTypes = OfficeDocFileTypes
            }).ConfigureAwait(true);
            if (pick == null)
                return;

            if (!TryGetDocumentOrVideoMime(pick.FileName, out var mime))
            {
                await DisplayAlert(Loc.T("chat.file"), Loc.T("chat.only_docs_video"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

            await using var stream = await pick.OpenReadAsync().ConfigureAwait(true);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(true);
            var bytes = ms.ToArray();
            AppLog.BinaryLoaded("document", pick.FileName, bytes.Length);
            if (bytes.Length == 0)
            {
                await DisplayAlert(Loc.T("chat.file"), Loc.T("chat.file_empty"), Loc.T("ok")).ConfigureAwait(true);
                return;
            }

            var sendName = pick.FileName;
            var isVideo = Video144pTranscoder.IsVideoMime(mime);
            if (!isVideo)
            {
                var headLen = Math.Min(4096, bytes.Length);
                if (!DocumentAttachHelper.SniffMatchesMime(bytes.AsSpan(0, headLen), mime))
                {
                    await DisplayAlert(Loc.T("chat.file"), Loc.T("chat.file_type_mismatch"), Loc.T("ok"))
                        .ConfigureAwait(true);
                    return;
                }
            }

            await SyncTrafficQualityAsync().ConfigureAwait(true);
            if (isVideo && MediaEconomy.UsesReducedMedia(_p2p))
            {
                var temp = Path.Combine(FileSystem.CacheDirectory,
                    $"iskra_in_{DateTime.UtcNow.Ticks}{Path.GetExtension(pick.FileName)}");
                await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(true);
                try
                {
                    var prepared = await Video144pTranscoder
                        .PrepareAsync(temp, pick.FileName, mime, MediaEconomy.Mode(_p2p))
                        .ConfigureAwait(true);
                    if (!prepared.Ok || prepared.Bytes == null)
                    {
                        await DisplayAlert(Loc.T("chat.video"),
                                prepared.Error ?? Loc.Tf("chat.video_transcode_fail",
                                    MediaEconomy.VideoResolutionLabel(_p2p)),
                                Loc.T("ok"))
                            .ConfigureAwait(true);
                        return;
                    }

                    bytes = prepared.Bytes;
                    mime = prepared.Mime;
                    sendName = prepared.FileName;
                    AppLog.BinaryLoaded("video-economy", sendName, bytes.Length);
                }
                finally
                {
                    try
                    {
                        File.Delete(temp);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
            else if (bytes.Length > _media.MaxDocumentBytes)
            {
                var limMb = (_media.MaxDocumentBytes + (1024 * 1024 - 1)) / (1024 * 1024);
                await DisplayAlert(Loc.T("chat.size"), Loc.Tf("chat.size_over", limMb), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

            if (bytes.Length > _media.MaxDocumentBytes)
            {
                await DisplayAlert(Loc.T("chat.size"), Loc.T("chat.size_still"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

            _media.ValidateDocumentMime(mime);
            await PrepareBinarySendAsync().ConfigureAwait(true);
            await _p2pSession.SendFileAsync(sendName, bytes, mime).ConfigureAwait(true);
            ClearDeliveryIssue();
        }
        catch (OutboundMessageQueuedException ex)
        {
            _logger.LogInformation(ex, "Document queued until peer is on LAN");
            ShowDeliveryIssue(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send document failed");
            ShowDeliveryIssue(ex.Message);
        }
        finally
        {
            await ReloadMessagesAsync().ConfigureAwait(true);
        }
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

        try
        {
            await OpenOrDownloadAttachmentAsync(vm.MessageId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open attachment failed");
            await DisplayAlert(Loc.T("chat.file"), ex.Message, Loc.T("ok")).ConfigureAwait(true);
        }
    }

    private async Task OpenOrDownloadAttachmentAsync(int messageId)
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

            var canDownload = _p2pSession != null &&
                              !row.Outgoing &&
                              !string.IsNullOrWhiteSpace(row.TransferId);
            if (!canDownload)
            {
                await DisplayAlert(Loc.T("chat.voice"), Loc.T("chat.voice_not_ready"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

            await DownloadThenShowAsync(messageId).ConfigureAwait(true);
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

        await DownloadThenShowAsync(messageId).ConfigureAwait(true);
    }

    private async Task DownloadThenShowAsync(int messageId)
    {
        if (_p2pSession == null)
            return;

        ClearDeliveryIssue();
        var downloaded = false;
        try
        {
            await MessengerServersBootstrap.EnsureRunningAsync(_p2p, _logger).ConfigureAwait(true);
            await _p2pSession.RequestBinaryDownloadAsync(messageId).ConfigureAwait(true);
            ClearDeliveryIssue();
            downloaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Transfer download failed in chat {ChatId}", ChatId);
            ShowDeliveryIssue(ex.Message);
        }
        finally
        {
            await ReloadMessagesAsync().ConfigureAwait(true);
        }

        var row = await WaitForMessageBlobAsync(messageId).ConfigureAwait(true);
        if (row?.ImageBlob is { Length: > 0 })
        {
            if (IsVoiceAttachment(row))
            {
                await PlayVoiceAttachmentAsync(row.ImageBlob).ConfigureAwait(true);
                return;
            }

            await DisplayAttachmentAsync(row).ConfigureAwait(true);
            return;
        }

        if (downloaded)
            await DisplayAlert(Loc.T("chat.file"), Loc.T("chat.download_retry"), Loc.T("ok")).ConfigureAwait(true);
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

    private async Task<ChatMessageEntity?> WaitForMessageBlobAsync(int messageId)
    {
        for (var i = 0; i < 8; i++)
        {
            var row = await _repo.GetMessageAsync(messageId).ConfigureAwait(true);
            if (row?.ImageBlob is { Length: > 0 })
                return row;
            await Task.Delay(120).ConfigureAwait(true);
        }

        return await _repo.GetMessageAsync(messageId).ConfigureAwait(true);
    }

    private async Task PrepareBinarySendAsync()
    {
        // Same probe as text: GetClients so servers-first PutBlob / SendMessage can find the peer.
        await MessengerServersBootstrap.EnsureRunningAsync(_p2p, _logger).ConfigureAwait(true);
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

    private async Task<(byte[] Bytes, string Mime)?> TryFitOutgoingImageAsync(
        byte[] bytes, string mime, bool askIfOverDefault)
    {
        await SyncTrafficQualityAsync().ConfigureAwait(true);
        var reduced = MediaEconomy.UsesReducedMedia(_p2p);
        var limit = MediaEconomy.ImageLimit(_media, _p2p);
        if (bytes.Length <= limit)
            return (bytes, mime);

        if (askIfOverDefault && !reduced)
        {
            var limKb = (limit + 1023) / 1024;
            var want = await DisplayAlert(Loc.T("chat.size"),
                Loc.Tf("chat.image_over", (bytes.Length + 1023) / 1024, limKb),
                Loc.T("chat.compress_action"),
                Loc.T("cancel")).ConfigureAwait(true);
            if (!want)
                return null;
        }

        if (!ImageAttachmentCompressor.TryCompressToMaxBytes(bytes, limit, out var compressed, out var err))
        {
            await DisplayAlert(Loc.T("chat.compress"), err ?? Loc.T("chat.compress_fail"), Loc.T("ok"))
                .ConfigureAwait(true);
            return null;
        }

        return (compressed, ImageAttachmentCompressor.SuggestMimeAfterCompression());
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

    private async void OnAttachCameraClicked(object? sender, EventArgs e)
    {
        if (_p2pSession == null)
            return;
        ClearDeliveryIssue();

        var photoLabel = Loc.T("preview.photo");
        var videoLabel = Loc.T("chat.video");
        var choice = await DisplayActionSheet(Loc.T("chat.camera"), Loc.T("cancel"), null, photoLabel, videoLabel)
            .ConfigureAwait(true);
        if (string.IsNullOrEmpty(choice) || choice == Loc.T("cancel"))
            return;

        if (choice == photoLabel)
            await CaptureAndSendCameraPhotoAsync().ConfigureAwait(true);
        else if (choice == videoLabel)
            await CaptureAndSendCameraVideoAsync().ConfigureAwait(true);
    }

    private async Task CaptureAndSendCameraPhotoAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_unsupported"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

            var cam = await Permissions.RequestAsync<Permissions.Camera>().ConfigureAwait(true);
            if (cam != PermissionStatus.Granted)
            {
                await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_perm"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

#if ANDROID
            await EnsureLegacyStorageWriteAsync().ConfigureAwait(true);
#endif

            var photo = await MediaPicker.Default.CapturePhotoAsync().ConfigureAwait(true);
            if (photo == null)
                return;

            await using var stream = await photo.OpenReadAsync().ConfigureAwait(true);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(true);
            var bytes = ms.ToArray();
            var fileName = string.IsNullOrWhiteSpace(photo.FileName)
                ? $"camera-{DateTime.UtcNow:yyyyMMdd-HHmmss}.jpg"
                : photo.FileName;
            AppLog.BinaryLoaded("camera-photo", fileName, bytes.Length);
            if (bytes.Length < 12)
            {
                await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_photo_fail"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

            if (!ImageAttachHelper.TryGetMimeFromExtension(fileName, out var mime))
                mime = "image/jpeg";

            var fitted = await TryFitOutgoingImageAsync(bytes, mime, askIfOverDefault: true).ConfigureAwait(true);
            if (fitted == null)
                return;
            bytes = fitted.Value.Bytes;
            mime = fitted.Value.Mime;

            _media.ValidateMime(mime);
            await PrepareBinarySendAsync().ConfigureAwait(true);
            await _p2pSession!.SendImageAsync(bytes, mime).ConfigureAwait(true);
            ClearDeliveryIssue();
        }
        catch (OutboundMessageQueuedException ex)
        {
            _logger.LogInformation(ex, "Camera photo queued until peer is on LAN");
            ShowDeliveryIssue(ex.Message);
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_unsupported"), Loc.T("ok"))
                .ConfigureAwait(true);
        }
        catch (PermissionException)
        {
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_perm"), Loc.T("ok"))
                .ConfigureAwait(true);
        }
        catch (FileNotFoundException ex) when (IsAppxManifestMissing(ex))
        {
            _logger.LogWarning(ex, "Camera photo failed: AppxManifest missing");
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_windows_manifest"), Loc.T("ok"))
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send camera photo failed");
            ShowDeliveryIssue(ex.Message);
        }
        finally
        {
            await ReloadMessagesAsync().ConfigureAwait(true);
        }
    }

    private async Task CaptureAndSendCameraVideoAsync()
    {
        try
        {
            // Same role as WinForms CameraRecordForm: record a camera video message.
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_unsupported"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

            var cam = await Permissions.RequestAsync<Permissions.Camera>().ConfigureAwait(true);
            if (cam != PermissionStatus.Granted)
            {
                await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_perm"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

            var mic = await Permissions.RequestAsync<Permissions.Microphone>().ConfigureAwait(true);
            if (mic != PermissionStatus.Granted)
            {
                await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_mic_perm"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

#if ANDROID
            await EnsureLegacyStorageWriteAsync().ConfigureAwait(true);
#endif

            var video = await MediaPicker.Default.CaptureVideoAsync().ConfigureAwait(true);
            if (video == null)
                return;

            await using var stream = await video.OpenReadAsync().ConfigureAwait(true);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(true);
            var bytes = ms.ToArray();
            var fileName = string.IsNullOrWhiteSpace(video.FileName)
                ? $"camera-{DateTime.UtcNow:yyyyMMdd-HHmmss}.mp4"
                : video.FileName;
            AppLog.BinaryLoaded("camera-video", fileName, bytes.Length);
            if (bytes.Length < 32)
            {
                await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_fail"), Loc.T("ok")).ConfigureAwait(true);
                return;
            }

            if (!TryGetDocumentOrVideoMime(fileName, out var mime))
                mime = "video/mp4";

            await SyncTrafficQualityAsync().ConfigureAwait(true);
            if (MediaEconomy.UsesReducedMedia(_p2p))
            {
                var temp = Path.Combine(FileSystem.CacheDirectory,
                    $"iskra_cam_{DateTime.UtcNow.Ticks}{Path.GetExtension(fileName)}");
                if (string.IsNullOrEmpty(Path.GetExtension(temp)))
                    temp += ".mp4";
                await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(true);
                try
                {
                    var prepared = await Video144pTranscoder
                        .PrepareAsync(temp, fileName, mime, MediaEconomy.Mode(_p2p))
                        .ConfigureAwait(true);
                    if (!prepared.Ok || prepared.Bytes == null)
                    {
                        await DisplayAlert(Loc.T("chat.video"),
                                prepared.Error ?? Loc.Tf("chat.video_transcode_fail",
                                    MediaEconomy.VideoResolutionLabel(_p2p)),
                                Loc.T("ok"))
                            .ConfigureAwait(true);
                        return;
                    }

                    bytes = prepared.Bytes;
                    mime = prepared.Mime;
                    fileName = prepared.FileName;
                    AppLog.BinaryLoaded("camera-video-economy", fileName, bytes.Length);
                }
                finally
                {
                    try
                    {
                        File.Delete(temp);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            }
            else if (bytes.Length > _media.MaxDocumentBytes)
            {
                var limMb = (_media.MaxDocumentBytes + (1024 * 1024 - 1)) / (1024 * 1024);
                await DisplayAlert(Loc.T("chat.size"), Loc.Tf("chat.size_over", limMb), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

            if (bytes.Length > _media.MaxDocumentBytes)
            {
                await DisplayAlert(Loc.T("chat.size"), Loc.T("chat.size_still"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }

            _media.ValidateDocumentMime(mime);
            await PrepareBinarySendAsync().ConfigureAwait(true);
            await _p2pSession!.SendFileAsync(fileName, bytes, mime).ConfigureAwait(true);
            ClearDeliveryIssue();
        }
        catch (OutboundMessageQueuedException ex)
        {
            _logger.LogInformation(ex, "Camera video queued until peer is on LAN");
            ShowDeliveryIssue(ex.Message);
        }
        catch (FeatureNotSupportedException)
        {
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_unsupported"), Loc.T("ok"))
                .ConfigureAwait(true);
        }
        catch (PermissionException)
        {
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_perm"), Loc.T("ok"))
                .ConfigureAwait(true);
        }
        catch (FileNotFoundException ex) when (IsAppxManifestMissing(ex))
        {
            _logger.LogWarning(ex, "Camera video failed: AppxManifest missing");
            await DisplayAlert(Loc.T("chat.camera"), Loc.T("chat.camera_windows_manifest"), Loc.T("ok"))
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send camera video failed");
            ShowDeliveryIssue(ex.Message);
        }
        finally
        {
            await ReloadMessagesAsync().ConfigureAwait(true);
        }
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