using Iskra.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Client.Services;
using ShortP2P.Client.Services.MessengerServers;

namespace Iskra.Maui;

/// <summary>
/// MAUI bootstrap so traffic goes servers-first like WinForms.
/// ShortP2P already prefers messenger servers in <c>DeliverOutgoingWireAsync</c> / long-poll inbox,
/// but <see cref="UserP2pRuntime.EnsureStartedAsync"/> skips <c>MessengerServers.Start()</c>
/// when LAN discovery fails — without Start(), receive never runs and send falls back to P2P.
/// </summary>
internal static class MessengerServersBootstrap
{
    private static int _serversRechecked;

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
            AppLog.Network.LogInformation("Messenger servers sync started");
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "MessengerServers.Start failed");
            return;
        }

        // Once per process: restore active/trusted for saved servers (inactive after downtime).
        if (Interlocked.CompareExchange(ref _serversRechecked, 1, 0) == 0)
            await RecheckSavedServersAsync(sync.Manager, logger, cancellationToken).ConfigureAwait(false);

        try
        {
            // JWT + GetClients — needed so TryDeliverWireAsync / PutBlob see the peer as registered.
            var clients = await sync.ProbeAndListRemoteClientsAsync(cancellationToken).ConfigureAwait(false);
            AppLog.ServerResponse("GetClients/probe", null, $"remoteClients={clients.Count}");
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

    /// <summary>Publish ChatRequest for a peer on all ready servers (session start may have been skipped).</summary>
    public static async Task PublishChatRequestAsync(
        UserP2pRuntime p2p,
        string peerNetworkIdShort,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var sync = p2p.MessengerServers;
        if (sync == null || string.IsNullOrWhiteSpace(peerNetworkIdShort))
            return;

        try
        {
            await EnsureRunningAsync(p2p, logger, cancellationToken).ConfigureAwait(false);
            await sync.PublishChatRequestAsync(peerNetworkIdShort.Trim(), cancellationToken).ConfigureAwait(false);
            AppLog.ServerResponse("ChatRequest", null, $"peer={peerNetworkIdShort.Trim()}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "PublishChatRequest failed for {PeerId}", peerNetworkIdShort);
        }
    }

    private static async Task RecheckSavedServersAsync(
        MessengerServerManager manager,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ShortP2P.Client.Data.MessengerServerEntity> servers;
        try
        {
            servers = await manager.ListAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "List messenger servers for recheck failed");
            return;
        }

        if (servers.Count == 0)
            return;

        logger?.LogInformation("Rechecking activity of {Count} messenger server(s)", servers.Count);

        await Parallel.ForEachAsync(
            servers,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Clamp(servers.Count, 1, 8),
                CancellationToken = cancellationToken
            },
            async (server, ct) =>
            {
                try
                {
                    await manager.RecheckServerAsync(server.Id, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger?.LogDebug(ex, "Recheck messenger server {BaseUrl} failed", server.BaseUrl);
                }
            }).ConfigureAwait(false);
    }
}
