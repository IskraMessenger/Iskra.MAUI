using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;

namespace Iskra.Maui;

public partial class ChatsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly List<ChatListRowVm> _allRows = [];
    private readonly ObservableCollection<ChatListRowVm> _chatRows = [];
    private readonly ChatRepository _chats;
    private readonly ILogger<ChatsPage> _logger;
    private readonly MessengerServerManager _messengerServers;
    private readonly UserP2pRuntime _p2p;
    private IDispatcherTimer? _presenceRefreshTimer;
    private string _search = "";

    public ChatsPage(AuthService auth, ChatRepository chats, UserP2pRuntime p2p,
        MessengerServerManager messengerServers, ILogger<ChatsPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _chats = chats;
        _p2p = p2p;
        _messengerServers = messengerServers;
        _logger = logger;
        ChatsCollection.ItemsSource = _chatRows;
    }

    private void OnChatListChangedFromInvite(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => _ = OnChatListChangedAsync());
    }

    private async Task OnChatListChangedAsync()
    {
        var ui = SynchronizationContext.Current;
        await RefreshAsync().ConfigureAwait(true);
        var u = _auth.CurrentUser;
        if (u == null)
            return;
        try
        {
            await _p2p.EnsureAllChatSessionsStartedAsync(u, _auth, _chats, ui, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ensure chat sessions after list change");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _chats.ChatListChanged -= OnChatListChangedFromInvite;
        _chats.ChatListChanged += OnChatListChangedFromInvite;
        _chats.ChatMessageAppended -= OnChatMessageAppended;
        _chats.ChatMessageAppended += OnChatMessageAppended;
        _p2p.LocalScan.ClientsChanged -= OnLanPresenceChanged;
        _p2p.LocalScan.ClientsChanged += OnLanPresenceChanged;
        _messengerServers.TrustThreatDetected -= OnMessengerServerTrustThreat;
        _messengerServers.TrustThreatDetected += OnMessengerServerTrustThreat;
        EnsurePresenceRefreshTimerStarted();
        var u = _auth.CurrentUser;
        if (u != null)
            try
            {
                await _p2p.EnsureStartedAsync(u).ConfigureAwait(true);
                await _p2p.EnsureAllChatSessionsStartedAsync(u, _auth, _chats, SynchronizationContext.Current)
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ensure P2P on chats page appearing");
            }

        await RefreshAsync().ConfigureAwait(true);
    }

    protected override void OnDisappearing()
    {
        _p2p.LocalScan.ClientsChanged -= OnLanPresenceChanged;
        _chats.ChatMessageAppended -= OnChatMessageAppended;
        _messengerServers.TrustThreatDetected -= OnMessengerServerTrustThreat;
        if (_presenceRefreshTimer != null)
            _presenceRefreshTimer.Stop();
        base.OnDisappearing();
    }

    private void OnChatMessageAppended(object? sender, ChatMessageAppendedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => _ = RefreshAsync());
    }

    private void OnMessengerServerTrustThreat(object? sender, MessengerServerTrustThreatEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await DisplayAlert(
                "Угроза безопасности",
                $"Сертификат сервера {e.Server.BaseUrl} не совпадает с сохранённым fingerprint.\n\n" +
                $"Ожидался: {e.ExpectedFingerprint}\nПолучен: {e.ActualFingerprint}\n\n" +
                "Сервер отключён и помечен как недоверенный.",
                "OK").ConfigureAwait(true);
        });
    }

    private void OnLanPresenceChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(UpdatePeerOnlineFlags);
    }

    private void UpdatePeerOnlineFlags()
    {
        foreach (var row in _allRows)
            row.IsPeerOnline = _p2p.LocalScan.IsPeerSeenRecentlyOnLan(row.Chat.PeerNetworkIdShort);
        Header.Bind(_auth.CurrentUser, _p2p);
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
        UpdatePeerOnlineFlags();
    }

    private async Task RefreshAsync()
    {
        var u = _auth.CurrentUser;
        if (u == null)
        {
            _chats.ChatListChanged -= OnChatListChangedFromInvite;
            _chats.ChatMessageAppended -= OnChatMessageAppended;
            _p2p.LocalScan.ClientsChanged -= OnLanPresenceChanged;
            Application.Current!.MainPage = new NavigationPage(MauiProgram.Services.GetRequiredService<LoginPage>());
            return;
        }

        Header.Bind(u, _p2p);
        var list = await _chats.ListChatsAsync(u.Id).ConfigureAwait(true);
        _allRows.Clear();
        foreach (var c in list)
        {
            var lastPage = await _chats.ListMessagesPageDescAsync(c.Id, 0, 1).ConfigureAwait(true);
            var last = lastPage.Count > 0 ? lastPage[0] : null;
            _allRows.Add(new ChatListRowVm(c, last, _p2p.LocalScan.IsPeerSeenRecentlyOnLan(c.PeerNetworkIdShort)));
        }

        ApplyFilter();
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _search = e.NewTextValue?.Trim() ?? "";
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<ChatListRowVm> src = _allRows;
        if (_search.Length > 0)
            src = _allRows.Where(r =>
                r.PeerNickname.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                r.PeerNetworkIdShort.Contains(_search, StringComparison.OrdinalIgnoreCase));

        _chatRows.Clear();
        foreach (var row in src)
            _chatRows.Add(row);
    }

    private async void OnAddChatClicked(object? sender, EventArgs e)
    {
        var page = MauiProgram.Services.GetRequiredService<AddChatPage>();
        await Navigation.PushModalAsync(new NavigationPage(page)).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
    }

    private async void OnChatSwipeDelete(object? sender, EventArgs e)
    {
        ChatEntity? chat = null;
        for (var p = sender as Element; p != null; p = p.Parent)
            if (p is SwipeView sw && sw.BindingContext is ChatListRowVm row)
            {
                chat = row.Chat;
                break;
            }

        if (chat == null)
            return;

        var u = _auth.CurrentUser;
        if (u == null)
            return;

        var confirm = await DisplayAlert("Удалить чат",
            $"Удалить «{chat.PeerNickname}» только на этом устройстве? Все сообщения будут удалены.",
            "Удалить", "Отмена").ConfigureAwait(true);
        if (!confirm)
            return;

        await _p2p.RemoveChatSessionAsync(chat.Id).ConfigureAwait(true);
        await _chats.DeleteChatAsync(chat.Id, u.Id).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
    }

    private async void OnChatSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ChatListRowVm row)
            return;

        ChatsCollection.SelectedItem = null;
        await ChatNav.OpenChatAsync(Navigation, row.Chat.Id).ConfigureAwait(true);
    }
}

public sealed class ChatListRowVm : INotifyPropertyChanged
{
    private bool _isPeerOnline;

    public ChatListRowVm(ChatEntity chat, ChatMessageEntity? last, bool isPeerOnline)
    {
        Chat = chat;
        _isPeerOnline = isPeerOnline;
        Initials = IskraTheme.Initials(chat.PeerNickname);
        AvatarColor = IskraTheme.AvatarColor(chat.PeerNetworkIdShort);
        (LastPreview, TimeLabel, DeliveryGlyph, DeliveryGlyphColor, ShowDelivery) = FromLast(last);
    }

    public ChatEntity Chat { get; }
    public string PeerNickname => Chat.PeerNickname;
    public string PeerNetworkIdShort => Chat.PeerNetworkIdShort;
    public string Initials { get; }
    public Color AvatarColor { get; }
    public string LastPreview { get; }
    public string TimeLabel { get; }
    public string DeliveryGlyph { get; }
    public Color DeliveryGlyphColor { get; }
    public bool ShowDelivery { get; }

    public bool IsPeerOnline
    {
        get => _isPeerOnline;
        set
        {
            if (_isPeerOnline == value)
                return;
            _isPeerOnline = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPeerOnline)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static (string Preview, string Time, string Glyph, Color GlyphColor, bool Show) FromLast(
        ChatMessageEntity? last)
    {
        if (last == null)
            return ("Нет сообщений", "", "", Colors.Transparent, false);

        var preview = ChatNav.Preview(last);
        var time = ChatNav.TimeLabel(last.SentUtcTicks);
        if (!last.Outgoing)
            return (preview, time, "", Colors.Transparent, false);

        var ds = (MessageDeliveryStatus)last.DeliveryStatus;
        if (ds == MessageDeliveryStatus.NotApplicable)
            ds = MessageDeliveryStatus.Delivered;
        return ds switch
        {
            MessageDeliveryStatus.Pending => (preview, time, "\u23f3", Color.FromArgb("#B8860B"), true),
            MessageDeliveryStatus.Failed => (preview, time, "!", Colors.Red, true),
            _ => (preview, time, "\u2713\u2713", IskraTheme.Check, true)
        };
    }
}
