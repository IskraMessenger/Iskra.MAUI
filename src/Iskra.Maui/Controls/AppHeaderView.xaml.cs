using ShortP2P.Auth.Data;
using ShortP2P.Client.Services;

namespace Iskra.Maui;

public partial class AppHeaderView : ContentView
{
    public AppHeaderView()
    {
        InitializeComponent();
    }

    public void Bind(UserEntity? user, UserP2pRuntime p2p)
    {
        PortLabel.Text = user == null ? "" : $"Порт: {user.DataUdpPort}";
        var meshOn = p2p.LocalScan.IsUdpListening || p2p.Settings.EnableUdpTransport;
        MeshDot.Fill = meshOn ? IskraTheme.Online : IskraTheme.Offline;
        MeshLabel.Text = meshOn ? "Mesh подключён" : "Mesh выключен";
        MeshLabel.TextColor = meshOn ? IskraTheme.Online : IskraTheme.Muted;
        var btOn = p2p.Settings.EnableBluetoothTransport && p2p.LocalScan.IsBluetoothListening;
        BtIcon.Opacity = btOn ? 1 : 0.28;
    }
}
