using Microsoft.Extensions.Logging;
using ShortP2P.Client.Services;

namespace Iskra.Maui;

/// <summary>
/// MAUI-side bootstrap for messenger-server sync.
/// ShortP2P <see cref="UserP2pRuntime.EnsureStartedAsync"/> skips <c>MessengerServers.Start()</c>
/// when LAN discovery fails — without Start(), long-poll (incoming offers) never runs.
/// </summary>
internal static class MessengerServersBootstrap
{
    public static async Task EnsureRunningAsync(
        UserP2pRuntime p2p,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var sync = p2p.MessengerServers;
        if (sync == null)
            return;

        try
        {
            sync.Start();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "MessengerServers.Start failed");
            return;
        }

        try
        {
            // Refreshes JWT + GetClients so PutBlob / SendMessage see the peer as registered.
            await sync.ProbeAndListRemoteClientsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Messenger server probe failed");
        }
    }
}
