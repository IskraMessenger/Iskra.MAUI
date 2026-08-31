using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Iskra.Maui.Localization;
using ShortP2P.Auth;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;
using ShortP2P.Transport.Abstractions;

namespace Iskra.Maui;

public sealed class ContactRow
{
    public ChatEntity? Chat { get; init; }
    public DiscoveredLocalPeer? Peer { get; init; }
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public required string Initials { get; init; }
    public required Color AvatarColor { get; init; }
    public bool IsOnline { get; init; }
}

public partial class ContactsPage : ContentPage
{
    private readonly List<ContactRow> _allRows = [];
    private readonly AuthService _auth;
    private readonly ChatRepository _chats;
    private readonly ILogger<ContactsPage> _logger;
    private readonly UserP2pRuntime _p2p;
    private readonly ObservableCollection<ContactRow> _rows = [];
    private bool _scanning;
    private string _search = "";

    public ContactsPage(AuthService auth, ChatRepository chats, UserP2pRuntime p2p, ILogger<ContactsPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _chats = chats;
        _p2p = p2p;
        _logger = logger;
        ContactsCollection.ItemsSource = _rows;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Title = Loc.T("tab.contacts");
        SearchEntry.Placeholder = Loc.T("contacts.search");
        ScanButton.Text = Loc.T("contacts.scan");
        EmptyContactsLabel.Text = Loc.T("contacts.empty");
        _p2p.LocalScan.ClientsChanged -= OnClientsChanged;
        _p2p.LocalScan.ClientsChanged += OnClientsChanged;
        var u = _auth.CurrentUser;
        if (u != null)
        {
            try
            {
                await _p2p.EnsureStartedAsync(u).ConfigureAwait(true);
                await MessengerServersBootstrap.EnsureRunningAsync(_p2p, _logger).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ensure P2P on contacts page appearing");
            }
        }

        await RefreshAsync().ConfigureAwait(true);
        _ = ProbeDiscoveryAsync();
    }

    protected override void OnDisappearing()
    {
        _p2p.LocalScan.ClientsChanged -= OnClientsChanged;
        base.OnDisappearing();
    }

    private void OnClientsChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() => _ = RefreshAsync());
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _search = e.NewTextValue?.Trim() ?? "";
        ApplyFilter();
    }

    private async Task ProbeDiscoveryAsync()
    {
        try
        {
            await _p2p.LocalScan.TriggerScanAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Contacts discovery round");
        }
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        if (_scanning)
            return;

        _scanning = true;
        ScanButton.IsEnabled = false;
        var sec = (int)Math.Round(LocalNetworkScanner.DefaultScanListenDuration.TotalSeconds);
        ScanStatusLabel.Text = Loc.Tf("contacts.scanning_detail", sec);
        ScanSpinner.IsRunning = true;
        ScanStatusRow.IsVisible = true;
        try
        {
            await _p2p.LocalScan.ScanAsync(LocalNetworkScanner.DefaultScanListenDuration).ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Contacts scan");
            await DisplayAlert(Loc.T("tab.contacts"), ex.Message, Loc.T("ok")).ConfigureAwait(true);
        }
        finally
        {
            _scanning = false;
            ScanSpinner.IsRunning = false;
            ScanStatusRow.IsVisible = false;
            ScanStatusLabel.Text = "";
            ScanButton.IsEnabled = true;
        }
    }

    private async Task RefreshAsync()
    {
        var u = _auth.CurrentUser;
        Header.Bind(u, _p2p);
        _allRows.Clear();
        if (u == null)
        {
            ApplyFilter();
            return;
        }

        var chats = await _chats.ListChatsAsync(u.Id).ConfigureAwait(true);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in chats)
        {
            seen.Add(ChatRepository.CanonicalPeerNetworkId(c.PeerNetworkIdShort));
            await TrySyncChatNicknameFromLanAsync(c).ConfigureAwait(true);
            _allRows.Add(new ContactRow
            {
                Chat = c,
                Name = c.PeerNickname,
                Detail = c.PeerNetworkIdShort,
                Initials = IskraTheme.Initials(c.PeerNickname),
                AvatarColor = IskraTheme.AvatarColor(c.PeerNetworkIdShort),
                IsOnline = IsOnline(c.PeerNetworkIdShort, null)
            });
        }

        foreach (var p in _p2p.LocalScan.Clients)
        {
            var id = ChatRepository.CanonicalPeerNetworkId(p.NetworkId.ToShortString());
            if (id.Length == 0 || seen.Contains(id))
                continue;
            seen.Add(id);
            var nick = string.IsNullOrWhiteSpace(p.Nickname) ? id : p.Nickname;
            _allRows.Add(new ContactRow
            {
                Peer = p,
                Name = nick,
                Detail = $"{id} · {TransportLabel(p)}",
                Initials = IskraTheme.Initials(nick),
                AvatarColor = IskraTheme.AvatarColor(id),
                IsOnline = IsOnline(id, p)
            });
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

    private bool IsOnline(string networkIdShort, DiscoveredLocalPeer? peer)
    {
        if (peer?.TransportKind == TransportKind.MessengerServer)
            return peer.MessengerServerOnline;
        return _p2p.LocalScan.IsPeerSeenRecentlyOnLan(networkIdShort) ||
               (peer?.MessengerServerOnline ?? false) ||
               _p2p.LocalScan.Clients.Any(c =>
                   ChatRepository.PeerNetworkIdsEqual(c.NetworkId.ToShortString(), networkIdShort) &&
                   c.MessengerServerOnline);
    }

    private void ApplyFilter()
    {
        IEnumerable<ContactRow> src = _allRows;
        if (_search.Length > 0)
            src = _allRows.Where(r =>
                r.Name.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
                r.Detail.Contains(_search, StringComparison.OrdinalIgnoreCase));

        _rows.Clear();
        foreach (var row in src)
            _rows.Add(row);
    }

    private static string TransportLabel(DiscoveredLocalPeer p) =>
        p.TransportKind switch
        {
            TransportKind.Udp => "LAN",
            TransportKind.Bluetooth => "Bluetooth",
            TransportKind.MessengerServer => Loc.T("network.servers"),
            _ => p.TransportKind.ToString()
        };

    private async void OnContactSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not ContactRow row)
            return;
        ContactsCollection.SelectedItem = null;
        try
        {
            if (row.Chat != null)
                await ChatNav.OpenChatAsync(this, row.Chat.Id).ConfigureAwait(true);
            else if (row.Peer != null)
                await ChatNav.OpenDiscoveredPeerAsync(this, row.Peer).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await DisplayAlert(Loc.T("tab.contacts"), ex.Message, Loc.T("ok")).ConfigureAwait(true);
        }
    }
}
