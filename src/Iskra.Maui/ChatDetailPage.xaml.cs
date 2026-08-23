using System.Collections.ObjectModel;
using System.Globalization;
using Iskra.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client;
using ShortP2P.Client.ChatMedia;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;

namespace Iskra.Maui;

public partial class ChatDetailPage : ContentPage
{
    private static readonly FilePickerFileType OfficeDocFileTypes = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.WinUI] =
            [
                ".doc", ".docx", ".rtf", ".pdf", ".odt", ".ods", ".odp", ".odg", ".xlsx", ".xls", ".pptx", ".ppt"
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
                "application/rtf"
            ],
            [DevicePlatform.iOS] = ["public.data"],
            [DevicePlatform.MacCatalyst] = ["public.data"]
        });

    private readonly AuthService _auth;
    private readonly ChatRepository _repo;
    private readonly UserP2pRuntime _p2p;
    private readonly ChatMediaOptions _media;
    private readonly ILogger<ChatDetailPage> _logger;
    private const int MessagesPageSize = 10;
    private readonly ObservableCollection<MessageRowVm> _messageItems = [];
    private readonly Dictionary<int, ImageSource> _imagePreviewCache = [];
    private readonly List<ChatMessageEntity> _loadedRows = [];
    private ChatP2PSession? _p2pSession;
    private string? _peerNetworkIdShort;
    private IDispatcherTimer? _presenceRefreshTimer;
    private bool _hasMoreRows = true;
    private bool _isLoadingRows;
    private bool _suppressLoadMore = true;
    private bool _pendingReload;
    private int _reloadEpoch;
    private const string VoiceMessageMime = "audio/ogg";
    private const string VoiceFileName = "voice.ogg";
#if ANDROID
    private global::Android.Media.MediaRecorder? _voiceRecorder;
    private string? _voiceTempPath;
#endif
    private bool _isVoiceRecording;

    public ChatDetailPage(AuthService auth, ChatRepository repo, UserP2pRuntime p2p, ChatMediaOptions media,
        ILogger<ChatDetailPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _repo = repo;
        _p2p = p2p;
        _media = media;
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

        _imagePreviewCache.Clear();
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
            var next = BuildMessageRowVm(page[i], previous);
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
        a.TimeLabel == b.TimeLabel &&
        ReferenceEquals(a.ImagePreview, b.ImagePreview);

    private MessageRowVm BuildMessageRowVm(ChatMessageEntity m, MessageRowVm? existing = null)
    {
        var color = m.Outgoing ? IskraTheme.SentText : IskraTheme.Text;
        var sentLocal = new DateTimeOffset(m.SentUtcTicks, TimeSpan.Zero).ToLocalTime();
        var ts = sentLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
        var ds = (MessageDeliveryStatus)m.DeliveryStatus;
        if (m.Outgoing && ds == MessageDeliveryStatus.NotApplicable)
            ds = MessageDeliveryStatus.Delivered;
        var (glyph, gColor, show) = DeliveryUiFor(ds, m.Outgoing);
        var bubble = m.Outgoing ? IskraTheme.OutgoingBubble : IskraTheme.IncomingBubble;

        if (m.PayloadKind == (int)ChatPayloadKind.File && m.ImageBlob is { Length: > 0 } docBlob)
        {
            var kb = (docBlob.Length + 1023) / 1024;
            var name = string.IsNullOrEmpty(m.Text) ? "file" : m.Text;
            var kindCaption = m.MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true
                ? "голосовое"
                : m.MimeType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true
                    ? "видео"
                    : "документ";
            return new MessageRowVm
            {
                CaptionLine = kindCaption,
                TextBody = "",
                FileBodyText = $"{name} · {kb} КБ · нажмите строку — Скачать",
                ShowTextBody = false,
                IsImage = false,
                IsFile = true,
                IsTransferOffer = false,
                MessageId = m.Id,
                ImagePreview = null,
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

        if (m.PayloadKind == (int)ChatPayloadKind.Image && m.ImageBlob is { Length: > 0 } blob)
        {
            return new MessageRowVm
            {
                CaptionLine = "фото",
                TextBody = "",
                ShowTextBody = false,
                IsImage = true,
                IsFile = false,
                IsTransferOffer = false,
                MessageId = m.Id,
                ImagePreview = ImagePreviewFor(m.Id, blob, existing),
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

        if (m.PayloadKind == (int)ChatPayloadKind.TransferOffer)
        {
            // Outgoing offers keep the local bytes — show them as the finished attachment (WinForms-style).
            if (m.Outgoing && m.ImageBlob is { Length: > 0 } localBlob)
            {
                var isImage = m.TransferPayloadKind.Equals("image", StringComparison.OrdinalIgnoreCase) ||
                              (m.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false);
                if (isImage)
                {
                    return new MessageRowVm
                    {
                        CaptionLine = "фото",
                        TextBody = "",
                        ShowTextBody = false,
                        IsImage = true,
                        IsFile = false,
                        IsTransferOffer = false,
                        MessageId = m.Id,
                        ImagePreview = ImagePreviewFor(m.Id, localBlob, existing),
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

                var kbLocal = (localBlob.Length + 1023) / 1024;
                var nameLocal = string.IsNullOrWhiteSpace(m.TransferFileName)
                    ? (string.IsNullOrEmpty(m.Text) ? "file" : m.Text)
                    : m.TransferFileName;
                var kindLocal = m.TransferPayloadKind.Equals("voice", StringComparison.OrdinalIgnoreCase) ||
                                (m.MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ?? false)
                    ? "голосовое"
                    : m.TransferPayloadKind.Equals("video", StringComparison.OrdinalIgnoreCase) ||
                      (m.MimeType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ?? false)
                        ? "видео"
                        : "документ";
                return new MessageRowVm
                {
                    CaptionLine = kindLocal,
                    TextBody = "",
                    FileBodyText = $"{nameLocal} · {kbLocal} КБ · нажмите строку — Скачать",
                    ShowTextBody = false,
                    IsImage = false,
                    IsFile = true,
                    IsTransferOffer = false,
                    MessageId = m.Id,
                    ImagePreview = null,
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

            var kb = (m.TransferSizeBytes + 1023) / 1024;
            var state = (ChatTransferState)m.TransferState;
            var stateText = state switch
            {
                ChatTransferState.Transferring => "загрузка...",
                ChatTransferState.Received => "получено",
                ChatTransferState.Failed => "ошибка, нажмите для повтора",
                _ => "нажмите строку — Скачать"
            };
            var name = string.IsNullOrWhiteSpace(m.TransferFileName) ? m.Text : m.TransferFileName;
            return new MessageRowVm
            {
                CaptionLine = m.TransferPayloadKind,
                TextBody = "",
                FileBodyText = $"{name} · {kb} КБ · {stateText}",
                ShowTextBody = false,
                IsImage = false,
                IsFile = true,
                IsTransferOffer = true,
                MessageId = m.Id,
                ImagePreview = null,
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

        return new MessageRowVm
        {
            CaptionLine = "",
            TextBody = m.Text,
            ShowTextBody = true,
            IsImage = false,
            IsFile = false,
            IsTransferOffer = false,
            MessageId = m.Id,
            ImagePreview = null,
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

    private ImageSource ImagePreviewFor(int messageId, byte[] blob, MessageRowVm? existing)
    {
        if (existing is { IsImage: true, ImagePreview: not null } && existing.MessageId == messageId)
            return existing.ImagePreview;
        if (_imagePreviewCache.TryGetValue(messageId, out var cached))
            return cached;

        var path = Path.Combine(FileSystem.CacheDirectory, $"iskra_img_{ChatId}_{messageId}");
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length != blob.Length)
                File.WriteAllBytes(path, blob);
            var source = ImageSource.FromFile(path);
            _imagePreviewCache[messageId] = source;
            return source;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Image preview cache write failed for {MessageId}", messageId);
            return ImageSource.FromStream(() => new MemoryStream(blob));
        }
    }

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

        if (_isVoiceRecording)
        {
            await StopVoiceRecordingAndSendAsync().ConfigureAwait(true);
            return;
        }

        await StartVoiceRecordingAsync().ConfigureAwait(true);
    }

    private async Task StartVoiceRecordingAsync()
    {
        ClearDeliveryIssue();
#if ANDROID
        var mic = await Permissions.RequestAsync<Permissions.Microphone>().ConfigureAwait(true);
        if (mic != PermissionStatus.Granted)
        {
            ShowDeliveryIssue("Нет разрешения на запись звука.");
            return;
        }

        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            ShowDeliveryIssue("Голосовые сообщения требуют Android 10+ (API 29).");
            return;
        }

        try
        {
            _voiceTempPath = Path.Combine(FileSystem.CacheDirectory, $"voice_{DateTime.UtcNow.Ticks}.ogg");
            var recorder = new global::Android.Media.MediaRecorder();
            _voiceRecorder = recorder;
            recorder.SetAudioSource(global::Android.Media.AudioSource.Mic);
            recorder.SetOutputFormat(global::Android.Media.OutputFormat.Ogg);
            recorder.SetAudioEncoder(global::Android.Media.AudioEncoder.Opus);
            recorder.SetAudioEncodingBitRate(18_000);
            recorder.SetAudioSamplingRate(48_000);
            recorder.SetOutputFile(_voiceTempPath);
            recorder.Prepare();
            recorder.Start();

            _isVoiceRecording = true;
            VoiceButton.Text = "■";
            VoiceButton.TextColor = Colors.Red;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Voice recording start failed");
            ShowDeliveryIssue(ex.Message);
            await StopVoiceRecordingAndDiscardAsync().ConfigureAwait(true);
        }
#else
        ShowDeliveryIssue("Запись голосовых сейчас доступна только на Android.");
#endif
    }

    private async Task StopVoiceRecordingAndSendAsync()
    {
#if ANDROID
        try
        {
            if (_voiceRecorder != null)
                _voiceRecorder.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Voice recording stop failed");
            ShowDeliveryIssue(ex.Message);
            await StopVoiceRecordingAndDiscardAsync().ConfigureAwait(true);
            return;
        }
        finally
        {
            try
            {
                _voiceRecorder?.Release();
                _voiceRecorder?.Dispose();
            }
            catch
            {
                // ignore
            }

            _voiceRecorder = null;
            _isVoiceRecording = false;
            VoiceButton.Text = "🎤";
            VoiceButton.TextColor = IskraTheme.Text;
        }

        try
        {
            if (string.IsNullOrEmpty(_voiceTempPath) || !File.Exists(_voiceTempPath))
            {
                ShowDeliveryIssue("Не удалось получить записанный голосовой файл.");
                return;
            }

            var bytes = await File.ReadAllBytesAsync(_voiceTempPath).ConfigureAwait(true);
            AppLog.BinaryLoaded("voice", VoiceFileName, bytes.Length);
            if (bytes.Length == 0)
            {
                ShowDeliveryIssue("Голосовая запись пустая.");
                return;
            }

            _media.ValidateDocumentMime(VoiceMessageMime);
            _media.ValidateDocumentSize(bytes.Length);
            await PrepareBinarySendAsync().ConfigureAwait(true);
            await _p2pSession!.SendFileAsync(VoiceFileName, bytes, VoiceMessageMime).ConfigureAwait(true);
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
        }
        finally
        {
            if (!string.IsNullOrEmpty(_voiceTempPath))
            {
                try
                {
                    File.Delete(_voiceTempPath);
                }
                catch
                {
                    // ignore
                }
            }

            _voiceTempPath = null;
            await ReloadMessagesAsync().ConfigureAwait(true);
        }
#else
        await Task.CompletedTask;
#endif
    }

    private async Task StopVoiceRecordingAndDiscardAsync()
    {
#if ANDROID
        try
        {
            if (_voiceRecorder != null)
            {
                try
                {
                    _voiceRecorder.Stop();
                }
                catch
                {
                    // ignore invalid state
                }

                _voiceRecorder.Release();
                _voiceRecorder.Dispose();
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            _voiceRecorder = null;
            _isVoiceRecording = false;
            VoiceButton.Text = "🎤";
            VoiceButton.TextColor = IskraTheme.Text;
            if (!string.IsNullOrEmpty(_voiceTempPath))
            {
                try
                {
                    File.Delete(_voiceTempPath);
                }
                catch
                {
                    // ignore
                }
            }

            _voiceTempPath = null;
        }
#endif
        await Task.CompletedTask.ConfigureAwait(true);
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

            if (bytes.Length > _media.MaxImageBytes)
            {
                var limKb = (_media.MaxImageBytes + 1023) / 1024;
                var want = await DisplayAlert("Размер",
                    $"Файл {(bytes.Length + 1023) / 1024} КБ больше лимита {limKb} КБ (настраивается в chat-media.json). Сжать изображение?",
                    "Сжать",
                    "Отмена").ConfigureAwait(true);
                if (!want)
                    return;
                if (!ImageAttachmentCompressor.TryCompressToMaxBytes(bytes, _media.MaxImageBytes, out var compressed,
                        out var err))
                {
                    await DisplayAlert("Сжатие", err ?? "Не удалось уложиться в лимит.", "OK").ConfigureAwait(true);
                    return;
                }

                bytes = compressed;
                mime = ImageAttachmentCompressor.SuggestMimeAfterCompression();
            }

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
                PickerTitle = "Документ Word / LibreOffice (до 10 МБ)",
                FileTypes = OfficeDocFileTypes
            }).ConfigureAwait(true);
            if (pick == null)
                return;

            if (!DocumentAttachHelper.TryGetMimeFromExtension(pick.FileName, out var mime))
            {
                await DisplayAlert("Файл",
                        "Допустимы только .doc, .docx, .rtf, .pdf, .odt, .ods, .odp, .odg, .xlsx, .xls, .pptx, .ppt.",
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

            var headLen = Math.Min(4096, bytes.Length);
            if (!DocumentAttachHelper.SniffMatchesMime(bytes.AsSpan(0, headLen), mime))
            {
                await DisplayAlert("Файл", "Содержимое не совпадает с типом файла.", "OK").ConfigureAwait(true);
                return;
            }

            if (bytes.Length > _media.MaxDocumentBytes)
            {
                var limMb = (_media.MaxDocumentBytes + (1024 * 1024 - 1)) / (1024 * 1024);
                await DisplayAlert("Размер", $"Файл больше {limMb} МБ (лимит maxDocumentBytes в chat-media.json).",
                        "OK")
                    .ConfigureAwait(true);
                return;
            }

            _media.ValidateDocumentMime(mime);
            await PrepareBinarySendAsync().ConfigureAwait(true);
            await _p2pSession.SendFileAsync(pick.FileName, bytes, mime).ConfigureAwait(true);
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
            await OpenReceivedBinaryAsync(vm.MessageId, vm.IsImage).ConfigureAwait(true);
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
        try
        {
            await MessengerServersBootstrap.EnsureRunningAsync(_p2p, _logger).ConfigureAwait(true);
            await _p2pSession.RequestBinaryDownloadAsync(messageId).ConfigureAwait(true);
            ClearDeliveryIssue();
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
    }

    private async Task PrepareBinarySendAsync()
    {
        // Same probe as text: GetClients so servers-first PutBlob / SendMessage can find the peer.
        await MessengerServersBootstrap.EnsureRunningAsync(_p2p, _logger).ConfigureAwait(true);
    }

    private async Task OpenReceivedBinaryAsync(int messageId, bool asImage)
    {
        var row = await _repo.GetMessageAsync(messageId).ConfigureAwait(true);
        if (row?.ImageBlob is not { Length: > 0 } blob)
        {
            await DisplayAlert("Файл", "Сообщение не найдено или пустое.", "OK").ConfigureAwait(true);
            return;
        }

        var fallback = asImage ? "image.jpg" : "document";
        var name = SanitizeFileName(string.IsNullOrEmpty(row.Text) ? fallback : row.Text);
        var temp = Path.Combine(FileSystem.CacheDirectory, $"{messageId}_{name}");
        await File.WriteAllBytesAsync(temp, blob).ConfigureAwait(true);
        AppLog.BinaryLoaded(asImage ? "received-image" : "received-document", name, blob.Length);
        if (asImage)
        {
            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                Title = "Изображение",
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
            if (bytes.Length > _media.MaxImageBytes)
            {
                if (!ImageAttachmentCompressor.TryCompressToMaxBytes(bytes, _media.MaxImageBytes, out var compressed,
                        out var err))
                {
                    await DisplayAlert("Сжатие", err ?? "Не удалось уложиться в лимит.", "OK").ConfigureAwait(true);
                    return;
                }

                bytes = compressed;
                mime = ImageAttachmentCompressor.SuggestMimeAfterCompression();
            }

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