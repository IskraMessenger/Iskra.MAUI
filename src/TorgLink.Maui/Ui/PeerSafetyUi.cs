using TorgLink.Maui.Localization;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.Crypto;

namespace TorgLink.Maui;

internal static class PeerSafetyUi
{
    public static string OwnHeader(AuthService auth, UserEntity user)
    {
        try
        {
            return $"{user.Nickname}: {SafetyNumber.FromPublicKey(auth.GetCurrentPublicKey())}";
        }
        catch
        {
            return "";
        }
    }

    public static string ChatPanel(AuthService auth, ChatEntity chat)
    {
        var user = auth.CurrentUser;
        if (user == null)
            return "";

        try
        {
            var fingerprints = PeerSafetyDisplay.FormatFingerprints(
                user.Nickname, auth.GetCurrentPublicKey(), chat.PeerNickname, chat.PeerRsaPublicJson);
            var channel = LocalizedChannel(chat);
            return channel.Length == 0
                ? fingerprints
                : fingerprints + Environment.NewLine + Loc.Tf("safety.channel", channel);
        }
        catch
        {
            return "";
        }
    }

    public static string LocalizedChannel(ChatEntity chat)
    {
        var kind = (chat.PeerKeySourceKind ?? "").Trim();
        if (kind.Length == 0)
            return "";
        if (string.Equals(kind, PeerKeySourceKinds.Udp, StringComparison.OrdinalIgnoreCase))
            return Loc.T("safety.ch_udp");
        if (string.Equals(kind, PeerKeySourceKinds.Bluetooth, StringComparison.OrdinalIgnoreCase))
            return Loc.T("safety.ch_bt");
        if (PeerKeySource.IsServer(kind))
        {
            var endpoint = PeerSafetyDisplay.FormatServerEndpoint(chat.PeerKeySourceDetail ?? "");
            return endpoint.Length == 0
                ? Loc.T("safety.ch_server")
                : Loc.Tf("safety.ch_server_ep", endpoint);
        }

        if (string.Equals(kind, PeerKeySourceKinds.Qr, StringComparison.OrdinalIgnoreCase))
            return Loc.T("safety.ch_qr");
        if (string.Equals(kind, PeerKeySourceKinds.Manual, StringComparison.OrdinalIgnoreCase))
            return Loc.T("safety.ch_manual");
        return kind;
    }

    public static async Task MarkUntrustedAsync(
        MessengerServerManager servers,
        ChatEntity? chat,
        ILogger logger,
        Page page)
    {
        var hint = chat != null && PeerKeySource.IsServer(chat.PeerKeySourceKind)
            ? chat.PeerKeySourceDetail
            : null;
        try
        {
            await servers.MarkUntrustedWithFailoverAsync(null, hint).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Emergency untrust failed");
            await page.DisplayAlert(Loc.T("safety.emergency"), ex.Message, Loc.T("ok")).ConfigureAwait(true);
        }
    }
}
