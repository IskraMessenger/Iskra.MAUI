using ShortP2P.Auth;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;
using Iskra.Maui.Localization;
using Iskra.Maui.Services;

namespace Iskra.Maui;

internal static class ChatNav
{
    public static async Task OpenChatAsync(Page host, int chatId)
    {
        var auth = MauiProgram.Services.GetRequiredService<AuthService>();
        var chats = MauiProgram.Services.GetRequiredService<ChatRepository>();
        var blacklist = MauiProgram.Services.GetRequiredService<PeerBlacklist>();
        var user = auth.CurrentUser;
        var chat = await chats.GetChatAsync(chatId).ConfigureAwait(true);
        if (chat != null && user != null)
        {
            await blacklist.EnsureLoadedAsync(user.Id).ConfigureAwait(true);
            if (blacklist.IsBlocked(user.Id, chat.PeerNetworkIdShort))
            {
                await host.DisplayAlert(Loc.T("blacklist.title"), Loc.T("blacklist.blocked"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }
        }

        var page = MauiProgram.Services.GetRequiredService<ChatDetailPage>();
        page.ChatId = chatId;
        await host.Navigation.PushAsync(page).ConfigureAwait(true);
    }

    public static async Task OpenDiscoveredPeerAsync(Page host, DiscoveredLocalPeer peer)
    {
        var auth = MauiProgram.Services.GetRequiredService<AuthService>();
        var chats = MauiProgram.Services.GetRequiredService<ChatRepository>();
        var p2p = MauiProgram.Services.GetRequiredService<UserP2pRuntime>();
        var blacklist = MauiProgram.Services.GetRequiredService<PeerBlacklist>();
        var user = auth.CurrentUser;
        if (user != null)
        {
            await blacklist.EnsureLoadedAsync(user.Id).ConfigureAwait(true);
            if (blacklist.IsBlocked(user.Id, peer.NetworkId.ToShortString()))
            {
                await host.DisplayAlert(Loc.T("blacklist.title"), Loc.T("blacklist.blocked"), Loc.T("ok"))
                    .ConfigureAwait(true);
                return;
            }
        }

        var result = await LanChatStartFromDiscovery
            .TryStartAsync(peer, auth, chats, p2p.CreateLanChatStartContext(), CancellationToken.None).ConfigureAwait(true);
        switch (result.Kind)
        {
            case LanChatStartKind.AlreadyExists:
            case LanChatStartKind.Created:
                if (result.Chat != null)
                    await OpenChatAsync(host, result.Chat.Id).ConfigureAwait(true);
                break;
            case LanChatStartKind.WaitingForPeer:
                await host.DisplayAlert(Loc.T("network.title"), result.Message ?? "", Loc.T("ok")).ConfigureAwait(true);
                break;
            case LanChatStartKind.Failed:
                await host.DisplayAlert(Loc.T("network.title"), result.Message ?? Loc.T("network.error"), Loc.T("ok"))
                    .ConfigureAwait(true);
                break;
        }
    }

    public static string Preview(ChatMessageEntity? m)
    {
        if (m == null)
            return Loc.T("preview.none");
        if (m.PayloadKind == (int)ChatPayloadKind.Image)
            return Loc.T("preview.photo");
        if (m.MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true)
            return Loc.T("preview.voice");
        if (m.PayloadKind is (int)ChatPayloadKind.File or (int)ChatPayloadKind.TransferOffer)
            return string.IsNullOrWhiteSpace(m.TransferFileName)
                ? (string.IsNullOrWhiteSpace(m.Text) ? Loc.T("preview.file") : m.Text)
                : m.TransferFileName;
        return string.IsNullOrWhiteSpace(m.Text) ? Loc.T("preview.message") : m.Text.Replace('\n', ' ');
    }

    public static string TimeLabel(long utcTicks)
    {
        if (utcTicks <= 0)
            return "";
        var local = new DateTimeOffset(utcTicks, TimeSpan.Zero).ToLocalTime();
        return local.Date == DateTime.Today
            ? local.ToString("HH:mm")
            : local.ToString("dd.MM");
    }
}
