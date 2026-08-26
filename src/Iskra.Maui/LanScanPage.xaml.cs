using System.Collections.ObjectModel;
using Iskra.Maui.Localization;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Client.Routing;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;
using ShortP2P.Transport.Abstractions;

namespace Iskra.Maui;

public sealed class LanScanRow
{
    public required DiscoveredLocalPeer Peer { get; init; }
    public string Nickname { get; init; } = "";
    public string NetworkIdShort { get; init; } = "";
    public string DetailLine { get; init; } = "";
    public bool IsPeerOnline { get; init; }

    public static LanScanRow From(DiscoveredLocalPeer p, bool isPeerOnline)
    {
        var idShort = p.NetworkId.ToShortString();
        var transport = p.TransportKind switch
        {
            TransportKind.Udp => "UDP",
            TransportKind.Bluetooth => "Bluetooth",
            TransportKind.Infrared => "IrDA",
            TransportKind.MessengerServer => Loc.T("network.servers"),
            _ => p.TransportKind.ToString()
        };
        var seen = p.LastSeenUtc.ToLocalTime().ToString("g");
        var presence = isPeerOnline ? Loc.T("online") : Loc.T("offline");
        return new LanScanRow
        {
            Peer = p,
            IsPeerOnline = isPeerOnline,
            Nickname = string.IsNullOrEmpty(p.Nickname) ? "—" : p.Nickname,
            NetworkIdShort = idShort,
            DetailLine = Loc.Tf("lan.detail", transport, presence, Loc.Tf("lan.last", seen))
        };
    }
}

public partial class LanScanPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ChatRepository _chats;
    private readonly ILogger<LanScanPage> _logger;
    private readonly UserP2pRuntime _p2p;
    private readonly ObservableCollection<LanScanRow> _rows = [];

    public LanScanPage(AuthService auth, ChatRepository chats, UserP2pRuntime p2p, ILogger<LanScanPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _chats = chats;
        _p2p = p2p;
        _logger = logger;
        PeerCollection.ItemsSource = _rows;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Title = Loc.T("lan.title");
        HintLabel.Text = Loc.T("lan.hint");
        ScanButton.Text = Loc.T("lan.scan");
        _p2p.LocalScan.ClientsChanged += OnClientsChanged;
        RefreshRows();
    }

    protected override void OnDisappearing()
    {
        _p2p.LocalScan.ClientsChanged -= OnClientsChanged;
        base.OnDisappearing();
    }

    private void OnClientsChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(RefreshRows);
    }

    private void RefreshRows()
    {
        _rows.Clear();
        foreach (var p in _p2p.LocalScan.Clients)
        {
            var idShort = p.NetworkId.ToShortString();
            var online = p.TransportKind == TransportKind.MessengerServer
                ? p.MessengerServerOnline
                : _p2p.LocalScan.IsPeerSeenRecentlyOnLan(idShort) || p.MessengerServerOnline;
            _rows.Add(LanScanRow.From(p, online));
        }
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        ScanButton.IsEnabled = false;
        var sec = (int)Math.Round(LocalNetworkScanner.DefaultScanListenDuration.TotalSeconds);
        StatusLabel.Text = Loc.Tf("lan.listening", sec);
        try
        {
            await _p2p.LocalScan.ScanAsync(LocalNetworkScanner.DefaultScanListenDuration).ConfigureAwait(true);
            RefreshRows();
        }
        finally
        {
            StatusLabel.Text = "";
            ScanButton.IsEnabled = true;
        }
    }

    private async void OnPeerDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Element el)
            return;
        var row = el.BindingContext as LanScanRow
                  ?? (el.Parent as Element)?.BindingContext as LanScanRow;
        if (row == null)
            return;

        try
        {
            var result = await LanChatStartFromDiscovery
                .TryStartAsync(row.Peer, _auth, _chats, _p2p, CancellationToken.None).ConfigureAwait(true);
            switch (result.Kind)
            {
                case LanChatStartKind.AlreadyExists:
                case LanChatStartKind.Created:
                    if (result.Chat != null)
                    {
                        var page = MauiProgram.Services.GetRequiredService<ChatDetailPage>();
                        page.ChatId = result.Chat.Id;
                        await Navigation.PushAsync(page).ConfigureAwait(true);
                    }

                    break;
                case LanChatStartKind.WaitingForPeer:
                    await DisplayAlert(Loc.T("lan.title"), result.Message ?? "", Loc.T("ok")).ConfigureAwait(true);
                    break;
                case LanChatStartKind.Failed:
                    await DisplayAlert(Loc.T("lan.title"), result.Message ?? Loc.T("error"), Loc.T("ok"))
                        .ConfigureAwait(true);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LAN scan peer activation");
            await DisplayAlert(Loc.T("lan.title"), ex.Message, Loc.T("ok")).ConfigureAwait(true);
        }
    }
}
