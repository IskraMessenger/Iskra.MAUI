using Iskra.Web.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using ShortP2P.Auth;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;

namespace Iskra.Web.Api.Services;

internal sealed class RealtimeForwarder : IHostedService
{
    private readonly ChatRepository _chats;
    private readonly UserP2pRuntime _p2p;
    private readonly MessengerServerManager _servers;
    private readonly PeerBlacklist _blacklist;
    private readonly IHubContext<IskraHub> _hub;

    public RealtimeForwarder(
        ChatRepository chats,
        UserP2pRuntime p2p,
        MessengerServerManager servers,
        PeerBlacklist blacklist,
        IHubContext<IskraHub> hub)
    {
        _chats = chats;
        _p2p = p2p;
        _servers = servers;
        _blacklist = blacklist;
        _hub = hub;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _chats.ChatListChanged += OnChats;
        _chats.ChatMessageAppended += OnMessage;
        _chats.PeerPublicKeyChanged += OnKey;
        _p2p.LocalScan.ClientsChanged += OnPresence;
        _servers.TrustThreatDetected += OnThreat;
        _servers.FailoverCompleted += OnFailover;
        _blacklist.Changed += OnChats;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _chats.ChatListChanged -= OnChats;
        _chats.ChatMessageAppended -= OnMessage;
        _chats.PeerPublicKeyChanged -= OnKey;
        _p2p.LocalScan.ClientsChanged -= OnPresence;
        _servers.TrustThreatDetected -= OnThreat;
        _servers.FailoverCompleted -= OnFailover;
        _blacklist.Changed -= OnChats;
        return Task.CompletedTask;
    }

    private void OnChats(object? sender, EventArgs e) =>
        _ = _hub.Clients.All.SendAsync("chatsChanged");

    private void OnMessage(object? sender, ChatMessageAppendedEventArgs e) =>
        _ = _hub.Clients.All.SendAsync("messagesChanged", e.ChatId);

    private void OnPresence(object? sender, EventArgs e) =>
        _ = _hub.Clients.All.SendAsync("presenceChanged");

    private void OnKey(object? sender, PeerPublicKeyChangedEventArgs e) =>
        _ = _hub.Clients.All.SendAsync("keyChanged", new
        {
            e.ChatId,
            e.PeerNickname,
            e.PreviousSafetyNumber,
            e.NewSafetyNumber
        });

    private void OnThreat(object? sender, MessengerServerTrustThreatEventArgs e) =>
        _ = _hub.Clients.All.SendAsync("trustThreat", new
        {
            baseUrl = e.Server.BaseUrl,
            e.ExpectedFingerprint,
            e.ActualFingerprint
        });

    private void OnFailover(object? sender, MessengerServerFailoverEventArgs e)
    {
        if (e.SwitchedToMesh)
            _ = _hub.Clients.All.SendAsync("meshFailover");
    }
}
