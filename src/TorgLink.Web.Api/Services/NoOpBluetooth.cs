using ShortP2P.Auth.Data;
using ShortP2P.Client.Bluetooth;
using ShortP2P.Client.Routing;
using ShortP2P.Discovery;
using ShortP2P.Transport;
using ShortP2P.Transport.Abstractions;

namespace TorgLink.Web.Api.Services;

internal sealed class NoOpBluetoothTransportProvider : IBluetoothTransportProvider
{
    public ITransport? Current => null;

    public void SetLocalNetworkId(CompressedNetworkId? networkId)
    {
    }

    public void ApplySettings(P2pRoutingSettings settings)
    {
    }
}

internal sealed class EmptyBluetoothRadioCatalog : IBluetoothRadioCatalog
{
    public ValueTask<IReadOnlyList<BluetoothRadioInfo>> ListRadiosAsync(
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<BluetoothRadioInfo>>([]);

    public ValueTask<string?> ResolveMacStringAsync(string? deviceId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<string?>(null);
}
