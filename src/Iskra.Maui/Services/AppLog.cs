using Microsoft.Extensions.Logging;

namespace Iskra.Maui.Services;

/// <summary>Named NLog categories for Iskra UI / network / files.</summary>
internal static class AppLog
{
    public static ILogger Ui { get; private set; } = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    public static ILogger Settings { get; private set; } = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    public static ILogger Files { get; private set; } = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    public static ILogger Network { get; private set; } = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    public static ILogger Server { get; private set; } = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    public static void Initialize(ILoggerFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Ui = factory.CreateLogger("Iskra.UI");
        Settings = factory.CreateLogger("Iskra.Settings");
        Files = factory.CreateLogger("Iskra.Files");
        Network = factory.CreateLogger("Iskra.Network");
        Server = factory.CreateLogger("Iskra.Server");
    }

    public static void PageOpened(string page, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            Ui.LogInformation("Page opened: {Page}", page);
        else
            Ui.LogInformation("Page opened: {Page} ({Title})", page, title);
    }

    public static void Button(string name, string? context = null)
    {
        if (string.IsNullOrWhiteSpace(context))
            Ui.LogInformation("Button: {Name}", name);
        else
            Ui.LogInformation("Button: {Name} ({Context})", name, context);
    }

    public static void SettingChanged(string key, object? value)
    {
        Settings.LogInformation("Setting changed: {Key}={Value}", key, value ?? "(null)");
    }

    public static void BinaryLoaded(string kind, string? name, long? bytes)
    {
        Files.LogInformation("Binary loaded: {Kind} name={Name} bytes={Bytes}", kind, name ?? "-", bytes);
    }

    public static void PeerConnected(string via, string? peerId)
    {
        Network.LogInformation("Peer connection: via={Via} peer={PeerId}", via, peerId ?? "-");
    }

    public static void ServerResponse(string action, string? url, object? detail)
    {
        Server.LogInformation("Server response: {Action} url={Url} {Detail}", action, url ?? "-", detail ?? "");
    }
}
