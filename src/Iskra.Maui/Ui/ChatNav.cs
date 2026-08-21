using ShortP2P.Auth;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Discovery;

namespace Iskra.Maui;

internal static class ChatNav
{
    public static async Task OpenChatAsync(INavigation navigation, int chatId)
    {
        var page = MauiProgram.Services.GetRequiredService<ChatDetailPage>();
        page.ChatId = chatId;
        await navigation.PushAsync(page).ConfigureAwait(true);
    }

    public static async Task OpenDiscoveredPeerAsync(Page host, DiscoveredLocalPeer peer)
    {
        var auth = MauiProgram.Services.GetRequiredService<AuthService>();
        var chats = MauiProgram.Services.GetRequiredService<ChatRepository>();
        var p2p = MauiProgram.Services.GetRequiredService<UserP2pRuntime>();
        var result = await LanChatStartFromDiscovery
            .TryStartAsync(peer, auth, chats, p2p, CancellationToken.None).ConfigureAwait(true);
        switch (result.Kind)
        {
            case LanChatStartKind.AlreadyExists:
            case LanChatStartKind.Created:
                if (result.Chat != null)
                    await OpenChatAsync(host.Navigation, result.Chat.Id).ConfigureAwait(true);
                break;
            case LanChatStartKind.WaitingForPeer:
                await host.DisplayAlert("Сеть", result.Message ?? "", "OK").ConfigureAwait(true);
                break;
            case LanChatStartKind.Failed:
                await host.DisplayAlert("Сеть", result.Message ?? "Ошибка", "OK").ConfigureAwait(true);
                break;
        }
    }

    public static string Preview(ChatMessageEntity? m)
    {
        if (m == null)
            return "Нет сообщений";
        if (m.PayloadKind == (int)ChatPayloadKind.Image)
            return "Фото";
        if (m.MimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            return "Голосовое сообщение";
        if (m.PayloadKind is (int)ChatPayloadKind.File or (int)ChatPayloadKind.TransferOffer)
            return string.IsNullOrWhiteSpace(m.TransferFileName)
                ? (string.IsNullOrWhiteSpace(m.Text) ? "Файл" : m.Text)
                : m.TransferFileName;
        return string.IsNullOrWhiteSpace(m.Text) ? "Сообщение" : m.Text.Replace('\n', ' ');
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
