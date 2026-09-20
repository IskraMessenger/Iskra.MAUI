using ShortP2P.Auth;
using ShortP2P.Client;
using ShortP2P.Client.Bluetooth;
using ShortP2P.Client.Services;
using ShortP2P.Crypto;
using ShortP2P.Transport;
using TorgLink.Maui.Localization;

namespace TorgLink.Maui;

internal static class ProfileShare
{
    public static async Task CopyAddressesAsync(Page host, AuthService auth, UserP2pRuntime p2p,
        IBluetoothRadioCatalog bluetoothCatalog)
    {
        var u = auth.CurrentUser;
        if (u == null)
            return;
        string? bt = null;
        try
        {
            bt = await BluetoothRoutingMac.GetEffectiveMacAsync(p2p.Settings, bluetoothCatalog)
                .ConfigureAwait(true);
        }
        catch
        {
            // ignore
        }

        var text = MyTransportEndpointsText.Build(u, p2p.Settings, bt);
        await Clipboard.Default.SetTextAsync(text).ConfigureAwait(true);
        await host.DisplayAlert(Loc.T("copied"), Loc.T("copied.addresses"), Loc.T("ok")).ConfigureAwait(true);
    }

    public static async Task CopyKeysAsync(Page host, AuthService auth)
    {
        var u = auth.CurrentUser;
        if (u == null)
            return;
        var pub = RsaKeySerializer.SerializePublic(auth.GetCurrentPublicKey());
        var text = $"Network id: {u.NetworkIdShort}\nPublic key JSON:\n{pub}";
        await Clipboard.Default.SetTextAsync(text).ConfigureAwait(true);
        await host.DisplayAlert(Loc.T("copied"), Loc.T("copied.keys"), Loc.T("ok")).ConfigureAwait(true);
    }
}
