using TorgLink.Web.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using ShortP2P.Auth;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;

namespace TorgLink.Web.Api.Services;

internal sealed class RealtimeForwarder : IHostedService
{
    private readonly ChatRepository _chats;
    private readonly UserP2pRuntime _p2p;
    private readonly MessengerServerManager _servers;
    private readonly PeerBlacklist _blacklist;
    private readonly IHubContext<TorgLinkHub> _hub;

    public RealtimeForwarder(
        ChatRepository chats,
        UserP2pRuntime p2p,
        MessengerServerManager servers,
        PeerBlacklist blacklist,
        IHubContext<TorgLinkHub> hub)
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
        _chats.ChatCreated += OnChatCreated;
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
        _chats.ChatCreated -= OnChatCreated;
        _chats.PeerPublicKeyChanged -= OnKey;
        _p2p.LocalScan.ClientsChanged -= OnPresence;
        _servers.TrustThreatDetected -= OnThreat;
        _servers.FailoverCompleted -= OnFailover;
        _blacklist.Changed -= OnChats;
        return Task.CompletedTask;
    }

    private void OnChats(object? sender, EventArgs e) =>
        _ = _hub.Clients.All.SendAsync("chatsChanged");

    private void OnMessage(object? sender, ChatMessageAppendedEventArgs e)
    {
        _ = _hub.Clients.All.SendAsync("messagesChanged", e.ChatId);
        if (!e.Outgoing)
            _ = NotifyIncomingMessageAsync(e.ChatId);
    }

    private void OnChatCreated(object? sender, ChatCreatedEventArgs e)
    {
        if (!e.Remote)
            return;
        _ = NotifyChatCreatedAsync(e.ChatId);
    }

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

    private async Task NotifyIncomingMessageAsync(int chatId)
    {
        try
        {
            var chat = await _chats.GetChatAsync(chatId).ConfigureAwait(false);
            if (chat == null)
                return;

            var last = (await _chats.ListMessagesPageDescAsync(chatId, 0, 1, includePayloadBlob: false)
                    .ConfigureAwait(false))
                .FirstOrDefault();
            var preview = ChatSessionHelper.Preview(last);
            var previewKind = PreviewKind(last);

            await _hub.Clients.All.SendAsync("incomingMessage", new
            {
                chatId,
                peerNickname = chat.PeerNickname,
                preview,
                previewKind
            }).ConfigureAwait(false);
        }
        catch
        {
            // UI already got messagesChanged; toast is best-effort
        }
    }

    private async Task NotifyChatCreatedAsync(int chatId)
    {
        try
        {
            var chat = await _chats.GetChatAsync(chatId).ConfigureAwait(false);
            if (chat == null)
                return;

            await _hub.Clients.All.SendAsync("chatCreated", new
            {
                chatId,
                peerNickname = chat.PeerNickname
            }).ConfigureAwait(false);
        }
        catch
        {
            // best-effort
        }
    }

    private static string PreviewKind(ChatMessageEntity? m)
    {
        if (m == null)
            return "none";
        if (m.PayloadKind == (int)ChatPayloadKind.Image)
            return "photo";
        if (m.MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true)
            return "voice";
        if (m.PayloadKind is (int)ChatPayloadKind.File or (int)ChatPayloadKind.TransferOffer)
            return "file";
        return "text";
    }
}
