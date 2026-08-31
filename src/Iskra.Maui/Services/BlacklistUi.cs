using Iskra.Maui.Localization;
using ShortP2P.Client.Services;

namespace Iskra.Maui.Services;

internal static class BlacklistUi
{
    public static async Task<bool> ConfirmAndBlockAsync(
        Page host,
        PeerBlacklist blacklist,
        int userId,
        string networkId,
        string nickname)
    {
        var id = ChatRepository.CanonicalPeerNetworkId(networkId);
        if (userId <= 0 || id.Length == 0)
            return false;

        await blacklist.EnsureLoadedAsync(userId).ConfigureAwait(true);
        if (blacklist.IsBlocked(userId, id))
            return true;

        var label = string.IsNullOrWhiteSpace(nickname) ? id : nickname.Trim();
        var ok = await host.DisplayAlert(
            Loc.T("blacklist.add_title"),
            Loc.Tf("blacklist.add_body", label),
            Loc.T("blacklist.add"),
            Loc.T("cancel")).ConfigureAwait(true);
        if (!ok)
            return false;

        await blacklist.AddAsync(userId, id, label).ConfigureAwait(true);
        return true;
    }
}
