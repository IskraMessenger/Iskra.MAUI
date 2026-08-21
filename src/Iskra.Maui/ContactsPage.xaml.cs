using System.Collections.ObjectModel;
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
    private readonly AuthService _auth;
    private readonly ChatRepository _chats;
    private readonly UserP2pRuntime _p2p;
    private readonly ObservableCollection<ContactRow> _rows = [];

    public ContactsPage(AuthService auth, ChatRepository chats, UserP2pRuntime p2p)
    {
        InitializeComponent();
        _auth = auth;
        _chats = chats;
        _p2p = p2p;
        ContactsCollection.ItemsSource = _rows;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _p2p.LocalScan.ClientsChanged -= OnClientsChanged;
        _p2p.LocalScan.ClientsChanged += OnClientsChanged;
        await RefreshAsync().ConfigureAwait(true);
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

    private async Task RefreshAsync()
    {
        var u = _auth.CurrentUser;
        Header.Bind(u, _p2p);
        _rows.Clear();
        if (u == null)
            return;

        var chats = await _chats.ListChatsAsync(u.Id).ConfigureAwait(true);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in chats)
        {
            seen.Add(c.PeerNetworkIdShort);
            _rows.Add(new ContactRow
            {
                Chat = c,
                Name = c.PeerNickname,
                Detail = c.PeerNetworkIdShort,
                Initials = IskraTheme.Initials(c.PeerNickname),
                AvatarColor = IskraTheme.AvatarColor(c.PeerNetworkIdShort),
                IsOnline = _p2p.LocalScan.IsPeerSeenRecentlyOnLan(c.PeerNetworkIdShort)
            });
        }

        foreach (var p in _p2p.LocalScan.Clients)
        {
            var id = p.NetworkId.ToShortString();
            if (seen.Contains(id))
                continue;
            var nick = string.IsNullOrWhiteSpace(p.Nickname) ? id : p.Nickname;
            var online = p.TransportKind == TransportKind.MessengerServer
                ? p.MessengerServerOnline
                : _p2p.LocalScan.IsPeerSeenRecentlyOnLan(id) || p.MessengerServerOnline;
            _rows.Add(new ContactRow
            {
                Peer = p,
                Name = nick,
                Detail = $"{id} · {TransportLabel(p)}",
                Initials = IskraTheme.Initials(nick),
                AvatarColor = IskraTheme.AvatarColor(id),
                IsOnline = online
            });
        }
    }

    private static string TransportLabel(DiscoveredLocalPeer p) =>
        p.TransportKind switch
        {
            TransportKind.Udp => "LAN",
            TransportKind.Bluetooth => "Bluetooth",
            TransportKind.MessengerServer => "сервер",
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
                await ChatNav.OpenChatAsync(Navigation, row.Chat.Id).ConfigureAwait(true);
            else if (row.Peer != null)
                await ChatNav.OpenDiscoveredPeerAsync(this, row.Peer).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Контакты", ex.Message, "OK").ConfigureAwait(true);
        }
    }
}
