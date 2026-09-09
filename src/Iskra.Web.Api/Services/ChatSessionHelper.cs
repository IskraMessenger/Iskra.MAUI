using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;

namespace Iskra.Web.Api.Services;

internal static class ChatSessionHelper
{
    public static async Task<ChatP2PSession?> EnsureSessionAsync(
        UserP2pRuntime p2p,
        AuthService auth,
        ChatRepository chats,
        ChatEntity chat,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var user = auth.CurrentUser;
        if (user == null)
            return null;

        await p2p.EnsureStartedAsync(user, cancellationToken).ConfigureAwait(false);
        await MessengerServersBootstrap.EnsureRunningAsync(p2p, logger, cancellationToken).ConfigureAwait(false);
        await MessengerServersBootstrap.PublishChatRequestAsync(p2p, chat.PeerNetworkIdShort, logger, cancellationToken)
            .ConfigureAwait(false);

        var session = p2p.GetSession(chat, user, auth, chats, null);
        if (!p2p.IsChatSessionStarted(chat.Id))
        {
            try
            {
                await session.StartAsync(cancellationToken).ConfigureAwait(false);
                p2p.MarkChatSessionStarted(chat.Id);
                AppLog.PeerConnected("chat-session", chat.PeerNetworkIdShort);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not start session for chat {ChatId}", chat.Id);
                throw;
            }
        }

        return session;
    }

    /// <summary>Attach session object without waiting for transport/handshake.</summary>
    public static ChatP2PSession? GetSessionOrNull(
        UserP2pRuntime p2p,
        AuthService auth,
        ChatRepository chats,
        ChatEntity chat)
    {
        var user = auth.CurrentUser;
        if (user == null)
            return null;
        return p2p.GetSession(chat, user, auth, chats, null);
    }

    /// <summary>Fire-and-forget P2P start so HTTP handlers (open chat / send text) stay responsive.</summary>
    public static void BeginEnsureSession(
        UserP2pRuntime p2p,
        AuthService auth,
        ChatRepository chats,
        ChatEntity chat,
        ILogger logger)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await EnsureSessionAsync(p2p, auth, chats, chat, logger).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background ensure session for chat {ChatId}", chat.Id);
            }
        });
    }

    public static async Task EnsureConnectivityAsync(
        UserP2pRuntime p2p,
        AuthService auth,
        ChatRepository chats,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var user = auth.CurrentUser;
        if (user == null)
            return;
        await p2p.EnsureStartedAsync(user, cancellationToken).ConfigureAwait(false);
        AppLog.PeerConnected("p2p-runtime", user.NetworkIdShort);
        await MessengerServersBootstrap.EnsureRunningAsync(p2p, logger, cancellationToken).ConfigureAwait(false);
        await p2p.EnsureAllChatSessionsStartedAsync(user, auth, chats, null, cancellationToken).ConfigureAwait(false);
    }

    public static string Initials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        var s = parts[0];
        return s.Length >= 2
            ? $"{char.ToUpperInvariant(s[0])}{char.ToUpperInvariant(s[1])}"
            : char.ToUpperInvariant(s[0]).ToString();
    }

    public static string AvatarColor(string key)
    {
        string[] colors =
        [
            "#5B8DEF", "#E39B2B", "#34C759", "#AF52DE", "#FF6B6B", "#32ADE6", "#FF9500", "#5856D6"
        ];
        if (string.IsNullOrEmpty(key))
            return colors[0];
        var hash = 0;
        foreach (var c in key)
            hash = (hash * 31) + c;
        return colors[Math.Abs(hash) % colors.Length];
    }

    public static string Preview(ChatMessageEntity? m)
    {
        if (m == null)
            return "";
        if (m.PayloadKind == (int)ChatPayloadKind.Image)
            return "photo";
        if (m.MimeType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true)
            return "voice";
        if (m.PayloadKind is (int)ChatPayloadKind.File or (int)ChatPayloadKind.TransferOffer)
            return string.IsNullOrWhiteSpace(m.TransferFileName)
                ? (string.IsNullOrWhiteSpace(m.Text) ? "file" : m.Text)
                : m.TransferFileName;
        return string.IsNullOrWhiteSpace(m.Text) ? "" : m.Text.Replace('\n', ' ');
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
