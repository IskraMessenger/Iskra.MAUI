using Microsoft.Extensions.Logging;
using ShortP2P.Client.Data;
using ShortP2P.Client.Services.MessengerServers;
using ShortP2P.MessengerServer.Contracts.Dtos;

namespace TorgLink.WinForms;

/// <summary>
/// Same role as Maui <c>MessengerServersBootstrap</c>: Start + one-shot recheck of saved servers,
/// then GetClients probe so discovery / delivery see registered peers.
/// </summary>
internal static class MessengerServersBootstrap
{
    private static int _serversRechecked;

    public static async Task<IReadOnlyList<ClientPresenceDto>> EnsureRunningAsync(
        MessengerServerSyncService sync,
        MessengerServerManager manager,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            sync.Start();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "MessengerServers.Start failed");
            return Array.Empty<ClientPresenceDto>();
        }

        if (Interlocked.CompareExchange(ref _serversRechecked, 1, 0) == 0)
            await RecheckSavedServersAsync(manager, logger, cancellationToken).ConfigureAwait(false);

        try
        {
            return await sync.ProbeAndListRemoteClientsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Messenger server GetClients probe failed");
            return Array.Empty<ClientPresenceDto>();
        }
    }

    private static async Task RecheckSavedServersAsync(
        MessengerServerManager manager,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MessengerServerEntity> servers;
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

        var tasks = new List<Task>(servers.Count);
        foreach (var server in servers)
        {
            var s = server;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await manager.RecheckServerAsync(s.Id, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger?.LogDebug(ex, "Recheck messenger server {BaseUrl} failed", s.BaseUrl);
                }
            }, cancellationToken));
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
    }
}
