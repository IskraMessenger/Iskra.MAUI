using System.Collections.ObjectModel;
using System.Globalization;
using Iskra.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client;
using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Data;
using ShortP2P.Client.Routing;
using ShortP2P.Client.Services;

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
    private readonly ILogger<ChatDetailPage> _logger;
    private const int MessagesPageSize = 10;
    private readonly ObservableCollection<MessageRowVm> _messageItems = [];
    private readonly List<ChatMessageEntity> _loadedRows = [];
    private ChatP2PSession? _p2pSession;
    private string? _peerNetworkIdShort;
    private IDispatcherTimer? _presenceRefreshTimer;
    private bool _hasMoreRows = true;
    private bool _isLoadingRows;
    private bool _suppressLoadMore = true;
    private bool _pendingReload;
    private int _reloadEpoch;
    private VoiceRecordingSession? _voice;

    public ChatDetailPage(AuthService auth, ChatRepository repo, UserP2pRuntime p2p, ChatMediaOptions media,
        P2pRoutingSettingsStore routingStore, ILogger<ChatDetailPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _repo = repo;
        _p2p = p2p;
        _media = media;
        _routingStore = routingStore;
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
            await DisplayAlert("Error", "Chat not found.", "OK").ConfigureAwait(true);
            await Navigation.PopAsync().ConfigureAwait(true);
            return;
        }

        Title = chat.PeerNickname;
        PeerNameLabel.Text = chat.PeerNickname;
        PeerAvatarInitials.Text = IskraTheme.Initials(chat.PeerNickname);
        PeerAvatarFill.BackgroundColor = IskraTheme.AvatarColor(chat.PeerNetworkIdShort);
        PeerIdLabel.Text = $"Узел: {chat.PeerNetworkIdShort}";
        _peerNetworkIdShort = chat.PeerNetworkIdShort;
        var user = _auth.CurrentUser;
        if (user == null)
        {
            _peerNetworkIdShort = null;
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
                await DisplayAlert("P2P", $"Could not start UDP: {ex.Message}", "OK").ConfigureAwait(true);
            }

        await ReloadMessagesAsync().ConfigureAwait(true);
        RefreshPeerPresenceLabel();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _ = StopVoiceRecordingAndDiscardAsync();
        _p2p.LocalScan.ClientsChanged -= OnPeerLanPresenceChanged;
        if (_presenceRefreshTimer != null)
            _presenceRefreshTimer.Stop();
        _peerNetworkIdShort = null;
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

    private void OnP2PMessagesChanged(object? sender, EventArgs e)
    {
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
            var page = await _repo.ListMessagesPageDescAsync(ChatId, 0, take).ConfigureAwait(true);
            ReleaseListPayloadBlobs(page);
            _hasMoreRows = page.Count == take;
            _loadedRows.Clear();
            _loadedRows.AddRange(page);
            SyncMessageItems(page);
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
            var page = await _repo.ListMessagesPageDescAsync(ChatId, _loadedRows.Count, MessagesPageSize)
                .ConfigureAwait(true);
            ReleaseListPayloadBlobs(page);
            _hasMoreRows = page.Count == MessagesPageSize;
            _loadedRows.AddRange(page);
            foreach (var m in page)
                _messageItems.Add(BuildMessageRowVm(m));
        }
        finally
        {
            _isLoadingRows = false;
        }
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
        a.IsTransferOffer == b.IsTransferOffer &&
        a.ShowDelivery == b.ShowDelivery &&
        a.DeliveryGlyph == b.DeliveryGlyph &&
        a.TimeLabel == b.TimeLabel;

    /// <summary>
    /// После выборки страницы сразу отпускаем BLOB: в списке они не нужны,
    /// открытие идёт через <see cref="OpenReceivedBinaryAsync"/>.
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
                ChatTransferState.Transferring => "загрузка...",
                ChatTransferState.Failed => "ошибка, нажмите для повтора",
                _ => "нажмите строку — Скачать"
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
            MessageId = m.Id,
            MessageColor = color,
            ShowDelivery = show,
            DeliveryGlyph = glyph,
            DeliveryGlyphColor = gColor,
            Outgoing = m.Outgoing,
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
        var action = stateText ?? "нажмите строку — Скачать";
        return new MessageRowVm
        {
            CaptionLine = AttachmentKindCaption(m),
            TextBody = "",
            FileBodyText = $"{name} · {kb} КБ · {action}",
            ShowTextBody = false,
            IsImage = IsImageAttachment(m),
            IsFile = true,
            IsTransferOffer = isTransferOffer,
            MessageId = m.Id,
            MessageColor = color,
            ShowDelivery = show,
            DeliveryGlyph = glyph,
            DeliveryGlyphColor = gColor,
            Outgoing = m.Outgoing,
            DeliveryStatus = deliveryStatus,
            BubbleColor = bubble,
            TimeLabel = ts
        };
    }

    private static int AttachmentSizeBytes(ChatMessageEntity m) =>
        (int)(m.ImageBlob is { Length: > 0 } blob ? blob.Length : m .TransferSizeBytes);

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
        if (IsImageAttachment(m))
            return "фото";
        if (string.Equals(m.TransferPayloadKind, "voice", StringComparison.OrdinalIgnoreCase) ||
            (m.MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ?? false))
            return "голосовое";
        if (IsVideoAttachment(m))
            return "видео";
        return "документ";
    }

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
    }

    private async void OnMessagesRemainingItemsThresholdReached(object? sender, EventArgs e)
    {
        if (_suppressLoadMore)
            return;
        await LoadNextMessagesPageAsync().ConfigureAwait(true);
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

    private async Task SyncUltraEconomyAsync()
    {
        var persisted = await _routingStore.LoadAsync().ConfigureAwait(true);
        MediaEconomy.Apply(_p2p, persisted.TrafficSavingEnabled);
    }

    private async Task StartVoiceRecordingAsync()
    {
        ClearDeliveryIssue();
        var mic = await Permissions.RequestAsync<Permissions.Microphone>().ConfigureAwait(true);
        if (mic != PermissionStatus.Granted)
        {
            ShowDeliveryIssue("Нет разрешения на запись звука.");
            return;
        }

        try
        {
            await SyncUltraEconomyAsync().ConfigureAwait(true);
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
                ShowDeliveryIssue("Не удалось получить записанный голосовой файл.");
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
                PickerTitle = "Изображение (JPEG, PNG, GIF)",
                FileTypes = FilePickerFileType.Images
            }).ConfigureAwait(true);
            if (pick == null)
                return;

            if (!ImageAttachHelper.TryGetMimeFromExtension(pick.FileName, out var mime))
            {
                await DisplayAlert("Файл", "Допустимы только .jpg, .jpeg, .png, .gif", "OK").ConfigureAwait(true);
                return;
            }

            await using var stream = await pick.OpenReadAsync().ConfigureAwait(true);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(true);
            var bytes = ms.ToArray();
            AppLog.BinaryLoaded("image", pick.FileName, bytes.Length);
            if (bytes.Length < 12)
            {
                await DisplayAlert("Файл", "Файл слишком маленький.", "OK").ConfigureAwait(true);
                return;
            }

            if (!ImageAttachHelper.SniffMatchesMime(bytes.AsSpan(0, Math.Min(12, bytes.Length)), mime))
            {
                await DisplayAlert("Файл", "Содержимое не совпадает с расширением файла.", "OK").ConfigureAwait(true);
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
                PickerTitle = "Документ или видео (до 10 МБ)",
                FileTypes = OfficeDocFileTypes
            }).ConfigureAwait(true);
            if (pick == null)
                return;

            if (!TryGetDocumentOrVideoMime(pick.FileName, out var mime))
            {
                await DisplayAlert("Файл",
                        "Допустимы офисные документы и видео (.mp4, .mov, .avi, .webm, .ogv, .wmv).",
                        "OK")
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
                await DisplayAlert("Файл", "Файл пустой.", "OK").ConfigureAwait(true);
                return;
            }

            var sendName = pick.FileName;
            var isVideo = Video144pTranscoder.IsVideoMime(mime);
            if (!isVideo)
            {
                var headLen = Math.Min(4096, bytes.Length);
                if (!DocumentAttachHelper.SniffMatchesMime(bytes.AsSpan(0, headLen), mime))
                {
                    await DisplayAlert("Файл", "Содержимое не совпадает с типом файла.", "OK").ConfigureAwait(true);
                    return;
                }
            }

            await SyncUltraEconomyAsync().ConfigureAwait(true);
            if (isVideo && MediaEconomy.IsEnabled(_p2p))
            {
                var temp = Path.Combine(FileSystem.CacheDirectory,
                    $"iskra_in_{DateTime.UtcNow.Ticks}{Path.GetExtension(pick.FileName)}");
                await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(true);
                try
                {
                    var prepared = await Video144pTranscoder.PrepareAsync(temp, pick.FileName, mime, true)
                        .ConfigureAwait(true);
                    if (!prepared.Ok || prepared.Bytes == null)
                    {
                        await DisplayAlert("Видео",
                                prepared.Error ?? "Не удалось перекодировать в 144p (256×144).",
                                "OK")
                            .ConfigureAwait(true);
                        return;
                    }

                    bytes = prepared.Bytes;
                    mime = prepared.Mime;
                    sendName = prepared.FileName;
                    AppLog.BinaryLoaded("video-144p", sendName, bytes.Length);
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
                await DisplayAlert("Размер", $"Файл больше {limMb} МБ (лимит maxDocumentBytes в chat-media.json).",
                        "OK")
                    .ConfigureAwait(true);
                return;
            }

            if (bytes.Length > _media.MaxDocumentBytes)
            {
                await DisplayAlert("Размер", "После сжатия видео всё ещё больше лимита вложения.", "OK")
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

        if (vm.IsTransferOffer)
        {
            await DownloadTransferOfferAsync(vm.MessageId).ConfigureAwait(true);
            return;
        }

        if (!vm.IsFile && !vm.IsImage)
            return;

        try
        {
            await OpenReceivedBinaryAsync(vm.MessageId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open attachment failed");
            await DisplayAlert("Файл", ex.Message, "OK").ConfigureAwait(true);
        }
    }

    private async Task DownloadTransferOfferAsync(int messageId)
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

        if (!downloaded)
            return;

        try
        {
            await OpenReceivedBinaryAsync(messageId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open attachment failed after download");
            await DisplayAlert("Файл", ex.Message, "OK").ConfigureAwait(true);
        }
    }

    private async Task PrepareBinarySendAsync()
    {
        // Same probe as text: GetClients so servers-first PutBlob / SendMessage can find the peer.
        await MessengerServersBootstrap.EnsureRunningAsync(_p2p, _logger).ConfigureAwait(true);
    }

    private async Task OpenReceivedBinaryAsync(int messageId)
    {
        var row = await _repo.GetMessageAsync(messageId).ConfigureAwait(true);
        if (row?.ImageBlob is not { Length: > 0 } blob)
        {
            await DisplayAlert("Файл", "Сообщение не найдено или пустое.", "OK").ConfigureAwait(true);
            return;
        }

        var isImage = IsImageAttachment(row);
        var isVideo = IsVideoAttachment(row);
        var fallback = isImage ? "image.jpg" : isVideo ? "video.mp4" : "document";
        var rawName = AttachmentDisplayName(row);
        var name = SanitizeFileName(string.IsNullOrWhiteSpace(rawName) ? fallback : rawName);
        var temp = Path.Combine(FileSystem.CacheDirectory, $"{messageId}_{name}");
        await File.WriteAllBytesAsync(temp, blob).ConfigureAwait(true);
        AppLog.BinaryLoaded(isImage ? "received-image" : isVideo ? "received-video" : "received-document", name, blob.Length);
        if (isImage || isVideo)
        {
            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                Title = isVideo ? "Видео" : "Изображение",
                File = new ReadOnlyFile(temp)
            }).ConfigureAwait(true);
            return;
        }

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Сохранить или отправить документ",
            File = new ShareFile(temp)
        }).ConfigureAwait(true);
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
        await SyncUltraEconomyAsync().ConfigureAwait(true);
        var ultra = MediaEconomy.IsEnabled(_p2p);
        var limit = ultra ? MediaEconomy.MaxImageBytes : _media.MaxImageBytes;
        if (bytes.Length <= limit)
            return (bytes, mime);

        if (askIfOverDefault && !ultra)
        {
            var limKb = (limit + 1023) / 1024;
            var want = await DisplayAlert("Размер",
                $"Файл {(bytes.Length + 1023) / 1024} КБ больше лимита {limKb} КБ. Сжать изображение?",
                "Сжать",
                "Отмена").ConfigureAwait(true);
            if (!want)
                return null;
        }

        if (!ImageAttachmentCompressor.TryCompressToMaxBytes(bytes, limit, out var compressed, out var err))
        {
            await DisplayAlert("Сжатие", err ?? "Не удалось уложиться в лимит.", "OK").ConfigureAwait(true);
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
        try
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync().ConfigureAwait(true);
            if (photo == null)
                return;

            await using var stream = await photo.OpenReadAsync().ConfigureAwait(true);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms).ConfigureAwait(true);
            var bytes = ms.ToArray();
            AppLog.BinaryLoaded("camera-image", photo.FileName, bytes.Length);
            if (bytes.Length < 12)
            {
                await DisplayAlert("Камера", "Не удалось получить снимок.", "OK").ConfigureAwait(true);
                return;
            }

            var mime = "image/jpeg";
            if (ImageAttachHelper.TryGetMimeFromExtension(photo.FileName, out var sniffed))
                mime = sniffed;
            var fitted = await TryFitOutgoingImageAsync(bytes, mime, askIfOverDefault: false).ConfigureAwait(true);
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
            _logger.LogInformation(ex, "Camera image queued until peer is on LAN");
            ShowDeliveryIssue(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Send camera image failed");
            ShowDeliveryIssue(ex.Message);
        }
        finally
        {
            await ReloadMessagesAsync().ConfigureAwait(true);
        }
    }

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
        PeerStatusLabel.Text = online ? "Онлайн" : "Офлайн";
        PeerStatusLabel.TextColor = online ? IskraTheme.Online : IskraTheme.Muted;
    }

    private void ShowDeliveryIssue(string message)
    {
        DeliveryIssueLabel.Text = string.IsNullOrWhiteSpace(message)
            ? "Проблема с доставкой текущего сообщения."
            : message.Trim();
        DeliveryIssueLabel.IsVisible = true;
    }

    private void ClearDeliveryIssue()
    {
        DeliveryIssueLabel.Text = string.Empty;
        DeliveryIssueLabel.IsVisible = false;
    }

    private async void OnClearChatClicked(object? sender, EventArgs e)
    {
        if (_p2pSession == null)
            return;

        var confirm = await DisplayAlert(
            "Удалить переписку",
            "Все сообщения будут удалены с этого устройства. Недоставленные отправки будут отменены.",
            "Удалить",
            "Отмена").ConfigureAwait(true);
        if (!confirm)
            return;

        ClearDeliveryIssue();
        try
        {
            var ok = await _p2pSession.ClearMessagesAsync().ConfigureAwait(true);
            if (!ok) await DisplayAlert("Ошибка", "Не удалось удалить переписку.", "OK").ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Clear chat failed for chat {ChatId}", ChatId);
            await DisplayAlert("Ошибка", ex.Message, "OK").ConfigureAwait(true);
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