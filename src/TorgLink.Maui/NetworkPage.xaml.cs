using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using TorgLink.Maui.Localization;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client.Qr;
using ShortP2P.Client.Services;
using ShortP2P.Crypto;
using ShortP2P.Discovery;
using ShortP2P.Transport;
using ShortP2P.Transport.Abstractions;

namespace TorgLink.Maui;

public sealed class NetworkNodeRow
{
    public required DiscoveredLocalPeer Peer { get; init; }
    public required string Name { get; init; }
    public required string IdShort { get; init; }
    public required string Initials { get; init; }
    public required Color AvatarColor { get; init; }
    public required string Status { get; init; }
    public required string Hops { get; init; }
}

public partial class NetworkPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly IBluetoothRadioCatalog _bluetoothCatalog;
    private readonly ILogger<NetworkPage> _logger;
    private readonly UserP2pRuntime _p2p;
    private readonly ObservableCollection<NetworkNodeRow> _nodes = [];

    public NetworkPage(AuthService auth, UserP2pRuntime p2p, IBluetoothRadioCatalog bluetoothCatalog,
        ILogger<NetworkPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _p2p = p2p;
        _bluetoothCatalog = bluetoothCatalog;
        _logger = logger;
        NodesCollection.ItemsSource = _nodes;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Title = Loc.T("tab.network");
        ApplyLocalizedUi();
        _p2p.LocalScan.ClientsChanged -= OnClientsChanged;
        _p2p.LocalScan.ClientsChanged += OnClientsChanged;
        Refresh();
    }

    private void ApplyLocalizedUi()
    {
        NodesTitle.Text = Loc.T("network.nodes");
        MyQrSectionLabel.Text = Loc.T("network.my_qr");
        AddChatButton.Text = Loc.T("network.add_chat");
        MyQrButton.Text = Loc.T("network.my_qr");
        MyAddressesButton.Text = Loc.T("network.my_addresses");
        CopyKeyButton.Text = Loc.T("network.copy_key");
        LanScanButton.Text = Loc.T("settings.lan");
        RoutingButton.Text = Loc.T("settings.routing");
        ServersButton.Text = Loc.T("network.servers");
    }

    protected override void OnDisappearing()
    {
        _p2p.LocalScan.ClientsChanged -= OnClientsChanged;
        base.OnDisappearing();
    }

    private void OnClientsChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(Refresh);

    private void Refresh()
    {
        var u = _auth.CurrentUser;
        Header.Bind(u, _p2p);
        _nodes.Clear();
        foreach (var p in _p2p.LocalScan.Clients)
        {
            var id = p.NetworkId.ToShortString();
            var nick = string.IsNullOrWhiteSpace(p.Nickname) ? id : p.Nickname;
            var online = p.TransportKind == TransportKind.MessengerServer
                ? p.MessengerServerOnline
                : _p2p.LocalScan.IsPeerSeenRecentlyOnLan(id) || p.MessengerServerOnline;
            _nodes.Add(new NetworkNodeRow
            {
                Peer = p,
                Name = nick,
                IdShort = id,
                Initials = TorgLinkTheme.Initials(nick),
                AvatarColor = TorgLinkTheme.AvatarColor(id),
                Status = online ? Loc.T("online") : Loc.T("offline"),
                Hops = p.TransportKind switch
                {
                    TransportKind.Udp => "1 hop",
                    TransportKind.Bluetooth => "1 hop",
                    TransportKind.MessengerServer => Loc.T("network.servers"),
                    _ => p.TransportKind.ToString()
                }
            });
        }

        NodesTitle.Text = Loc.Tf("network.nodes_count", _nodes.Count);
        RenderQr(u);
    }

    private void RenderQr(UserEntity? u)
    {
        if (u == null)
            return;
        try
        {
            var pub = RsaKeySerializer.SerializePublic(_auth.GetCurrentPublicKey());
            var png = PeerQrService.EncodeQrPng(PeerQrService.BuildPayload(u, pub));
            QrImage.Source = ImageSource.FromStream(() => new MemoryStream(png));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Render network QR");
        }
    }

    private async void OnNodeTapped(object? sender, TappedEventArgs e)
    {
        NetworkNodeRow? row = null;
        for (var el = sender as Element; el != null; el = el.Parent)
            if (el.BindingContext is NetworkNodeRow n)
            {
                row = n;
                break;
            }

        if (row == null)
            return;
        try
        {
            await ChatNav.OpenDiscoveredPeerAsync(this, row.Peer).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await DisplayAlert(Loc.T("tab.network"), ex.Message, Loc.T("ok")).ConfigureAwait(true);
        }
    }

    private async void OnAddChatClicked(object? sender, EventArgs e)
    {
        var page = MauiProgram.Services.GetRequiredService<AddChatPage>();
        await Navigation.PushModalAsync(new NavigationPage(page)).ConfigureAwait(true);
    }

    private async void OnMyQrClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(MauiProgram.Services.GetRequiredService<MyQrPage>()).ConfigureAwait(true);

    private async void OnMyAddressesClicked(object? sender, EventArgs e) =>
        await ProfileShare.CopyAddressesAsync(this, _auth, _p2p, _bluetoothCatalog).ConfigureAwait(true);

    private async void OnCopyKeysClicked(object? sender, EventArgs e) =>
        await ProfileShare.CopyKeysAsync(this, _auth).ConfigureAwait(true);

    private async void OnLanScanClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(MauiProgram.Services.GetRequiredService<LanScanPage>()).ConfigureAwait(true);

    private async void OnRoutingClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(MauiProgram.Services.GetRequiredService<RoutingSettingsPage>()).ConfigureAwait(true);

    private async void OnServersClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(MauiProgram.Services.GetRequiredService<MessengerServersPage>()).ConfigureAwait(true);
}
