using System.Collections.ObjectModel;
using System.ComponentModel;
using Iskra.Maui.Localization;
using Iskra.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
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
    private readonly PeerBlacklist _blacklist;
    private readonly UserP2pRuntime _p2p;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private int _refreshPending;
    private int _ignoreSelection;
    private IDispatcherTimer? _presenceRefreshTimer;
    private Task _connectivityTask = Task.CompletedTask;
    private string _search = "";

    public ChatsPage(AuthService auth, ChatRepository chats, UserP2pRuntime p2p,
        MessengerServerManager messengerServers, PeerBlacklist blacklist, ILogger<ChatsPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _chats = chats;
        _p2p = p2p;
        _messengerServers = messengerServers;
        _blacklist = blacklist;
        _logger = logger;
        ChatsCollection.ItemsSource = _chatRows;
    }

    private void OnChatListChangedFromInvite(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => _ = OnChatListChangedAsync());
    }

    private async Task OnChatListChangedAsync()
    {
        await RefreshAsync().ConfigureAwait(true);
        var u = _auth.CurrentUser;
        if (u == null)
            return;
        _ = EnsureSessionsAfterChatListChangedAsync(u);
    }

    private async Task EnsureSessionsAfterChatListChangedAsync(UserEntity u)
    {
        try
        {
            await _p2p.EnsureAllChatSessionsStartedAsync(u, _auth, _chats, null, CancellationToken.None)
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
        Title = Loc.T("tab.chats");
        SearchEntry.Placeholder = Loc.T("search");
        EmptyChatsLabel.Text = Loc.T("chats.empty");
        _chats.ChatListChanged -= OnChatListChangedFromInvite;
        _chats.ChatListChanged += OnChatListChangedFromInvite;
        _chats.ChatMessageAppended -= OnChatMessageAppended;
        _chats.ChatMessageAppended += OnChatMessageAppended;
        _p2p.LocalScan.ClientsChanged -= OnLanPresenceChanged;
        _p2p.LocalScan.ClientsChanged += OnLanPresenceChanged;
        _messengerServers.TrustThreatDetected -= OnMessengerServerTrustThreat;
        _messengerServers.TrustThreatDetected += OnMessengerServerTrustThreat;
        _blacklist.Changed -= OnBlacklistChanged;
        _blacklist.Changed += OnBlacklistChanged;
        EnsurePresenceRefreshTimerStarted();
        var u = _auth.CurrentUser;
        if (u != null)
        {
            try
            {
                await _blacklist.EnsureLoadedAsync(u.Id).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Load blacklist on chats page appearing");
            }
        }

        // Always paint from local SQLite first. P2P / servers / sessions must not gate the list.
        await RefreshAsync().ConfigureAwait(true);

        if (u != null)
            QueueConnectivity(u);
    }

    private void QueueConnectivity(UserEntity u)
    {
        if (!_connectivityTask.IsCompleted)
            return;
        _connectivityTask = EnsureConnectivityInBackgroundAsync(u);
    }

    private async Task EnsureConnectivityInBackgroundAsync(UserEntity u)
    {
        try
        {
            await _p2p.EnsureStartedAsync(u).ConfigureAwait(false);
            AppLog.PeerConnected("p2p-runtime", u.NetworkIdShort);
            await MessengerServersBootstrap.EnsureRunningAsync(_p2p, _logger).ConfigureAwait(false);
            await _p2p.EnsureAllChatSessionsStartedAsync(u, _auth, _chats, null).ConfigureAwait(false);
            MainThread.BeginInvokeOnMainThread(UpdatePeerOnlineFlags);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ensure P2P on chats page appearing");
        }
    }

    protected override void OnDisappearing()
    {
        _p2p.LocalScan.ClientsChanged -= OnLanPresenceChanged;
        _chats.ChatMessageAppended -= OnChatMessageAppended;
        _messengerServers.TrustThreatDetected -= OnMessengerServerTrustThreat;
        _blacklist.Changed -= OnBlacklistChanged;
        if (_presenceRefreshTimer != null)
            _presenceRefreshTimer.Stop();
        base.OnDisappearing();
    }

    private void OnBlacklistChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => _ = RefreshAsync());
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
                Loc.T("security.threat_title"),
                Loc.Tf("security.threat_body", e.Server.BaseUrl, e.ExpectedFingerprint, e.ActualFingerprint),
                Loc.T("ok")).ConfigureAwait(true);
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
        if (!await _refreshGate.WaitAsync(0).ConfigureAwait(true))
        {
            Interlocked.Exchange(ref _refreshPending, 1);
            return;
        }

        try
        {
            do
            {
                Interlocked.Exchange(ref _refreshPending, 0);
                await RefreshCoreAsync().ConfigureAwait(true);
            } while (Interlocked.Exchange(ref _refreshPending, 0) == 1);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RefreshCoreAsync()
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
        // Local SQLite only — presence affects the green dot, never membership.
        var list = await _chats.ListChatsAsync(u.Id).ConfigureAwait(true);
        _allRows.Clear();
        foreach (var c in list)
        {
            await TrySyncChatNicknameFromLanAsync(c).ConfigureAwait(true);
            var lastPage = await _chats.ListMessagesPageDescAsync(c.Id, 0, 1).ConfigureAwait(true);
            var last = lastPage.Count > 0 ? lastPage[0] : null;
            _allRows.Add(new ChatListRowVm(c, last, _p2p.LocalScan.IsPeerSeenRecentlyOnLan(c.PeerNetworkIdShort)));
        }

        ApplyFilter();
    }

    private async Task TrySyncChatNicknameFromLanAsync(ChatEntity chat)
    {
        if (!ChatRepository.IsPlaceholderNickname(chat.PeerNickname, chat.PeerNetworkIdShort))
            return;

        var id = ChatRepository.CanonicalPeerNetworkId(chat.PeerNetworkIdShort);
        foreach (var p in _p2p.LocalScan.Clients)
        {
            if (!ChatRepository.PeerNetworkIdsEqual(p.NetworkId.ToShortString(), id))
                continue;
            var nick = p.Nickname?.Trim() ?? "";
            if (ChatRepository.IsPlaceholderNickname(nick, id))
                continue;
            if (await _chats.TryUpdatePeerNicknameAsync(chat.Id, nick).ConfigureAwait(true))
                chat.PeerNickname = nick;
            return;
        }
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _search = e.NewTextValue?.Trim() ?? "";
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<ChatListRowVm> src = _allRows.Where(r =>
            !_blacklist.IsBlocked(_auth.CurrentUser?.Id, r.PeerNetworkIdShort));
        if (_search.Length > 0)
            src = src.Where(r =>
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

        var confirm = await DisplayAlert(Loc.T("chats.delete_title"),
            Loc.Tf("chats.delete_body", chat.PeerNickname),
            Loc.T("delete"), Loc.T("cancel")).ConfigureAwait(true);
        if (!confirm)
            return;

        await _p2p.RemoveChatSessionAsync(chat.Id).ConfigureAwait(true);
        await _chats.DeleteChatAsync(chat.Id, u.Id).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
    }

    private async void OnBlockChatClicked(object? sender, EventArgs e)
    {
        Interlocked.Exchange(ref _ignoreSelection, 1);
        ChatEntity? chat = null;
        for (var p = sender as Element; p != null; p = p.Parent)
            if (p.BindingContext is ChatListRowVm row)
            {
                chat = row.Chat;
                break;
            }

        var u = _auth.CurrentUser;
        if (chat == null || u == null)
            return;

        await BlacklistUi.ConfirmAndBlockAsync(this, _blacklist, u.Id, chat.PeerNetworkIdShort, chat.PeerNickname)
            .ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
    }

    private async void OnChatSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (Interlocked.Exchange(ref _ignoreSelection, 0) == 1)
        {
            ChatsCollection.SelectedItem = null;
            return;
        }

        if (e.CurrentSelection.FirstOrDefault() is not ChatListRowVm row)
            return;

        ChatsCollection.SelectedItem = null;
        await ChatNav.OpenChatAsync(this, row.Chat.Id).ConfigureAwait(true);
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
