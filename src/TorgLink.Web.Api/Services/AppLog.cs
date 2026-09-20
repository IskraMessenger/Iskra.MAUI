using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TorgLink.Web.Api.Services;

internal static class AppLog
{
    public static ILogger Ui { get; private set; } = NullLogger.Instance;
    public static ILogger Settings { get; private set; } = NullLogger.Instance;
    public static ILogger Files { get; private set; } = NullLogger.Instance;
    public static ILogger Network { get; private set; } = NullLogger.Instance;
    public static ILogger Server { get; private set; } = NullLogger.Instance;

    public static void Initialize(ILoggerFactory factory)
    {
        Ui = factory.CreateLogger("TorgLink.UI");
        Settings = factory.CreateLogger("TorgLink.Settings");
        Files = factory.CreateLogger("TorgLink.Files");
        Network = factory.CreateLogger("TorgLink.Network");
        Server = factory.CreateLogger("TorgLink.Server");
    }

    public static void SettingChanged(string key, object? value) =>
        Settings.LogInformation("Setting changed: {Key}={Value}", key, value ?? "(null)");

    public static void BinaryLoaded(string kind, string? name, long? bytes) =>
        Files.LogInformation("Binary loaded: {Kind} name={Name} bytes={Bytes}", kind, name ?? "-", bytes);

    public static void PeerConnected(string via, string? peerId) =>
        Network.LogInformation("Peer connection: via={Via} peer={PeerId}", via, peerId ?? "-");

    public static void ServerResponse(string action, string? url, object? detail) =>
        Server.LogInformation("Server response: {Action} url={Url} {Detail}", action, url ?? "-", detail ?? "");
}
