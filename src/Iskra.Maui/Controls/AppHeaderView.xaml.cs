using ShortP2P.Auth.Data;
using ShortP2P.Client.Services;
using Iskra.Maui.Localization;

namespace Iskra.Maui;

public partial class AppHeaderView : ContentView
{
    public AppHeaderView()
    {
        InitializeComponent();
    }

    public void Bind(UserEntity? user, UserP2pRuntime p2p)
    {
        NickLabel.Text = user?.Nickname ?? "";
        PortLabel.Text = user == null ? "" : Loc.Tf("header.port", user.DataUdpPort);
        var meshOn = p2p.LocalScan.IsUdpListening || p2p.Settings.EnableUdpTransport;
        MeshDot.Fill = meshOn ? IskraTheme.Online : IskraTheme.Offline;
        MeshLabel.Text = meshOn ? Loc.T("header.mesh_on") : Loc.T("header.mesh_off");
        MeshLabel.TextColor = meshOn ? IskraTheme.Online : IskraTheme.Muted;
        var btOn = p2p.Settings.EnableBluetoothTransport && p2p.LocalScan.IsBluetoothListening;
        BtIcon.Opacity = btOn ? 1 : 0.28;
    }
}
