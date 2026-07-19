// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using System.Threading.Tasks;
using Robust.Client.Utility;
using Robust.Shared.IoC;

namespace Content.Client.Launcher;

/// <summary>
/// Polls the public HTTP /status endpoint while the connecting screen is open.
/// HTTP itself runs in the engine (<see cref="IGameServerStatusClient"/>) to satisfy the client sandbox.
/// Requires matching Robust.Client that ships <see cref="IGameServerStatusClient"/> (ministation RT 283+).
/// </summary>
public sealed class ConnectingServerStatusFetcher : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IGameServerStatusClient? _statusClient;
    private CancellationTokenSource? _loopCts;
    private bool _disposed;

    public event Action<ConnectingServerStatus?>? StatusUpdated;

    public ConnectingServerStatusFetcher()
    {
        // Engine without this API / missing IoC registration → UI stays on "unavailable".
        IoCManager.Instance?.TryResolveType(out _statusClient);
    }

    /// <param name="connectAddress">
    /// ss14://host:port, host:port, or similar — parsed by the engine helper.
    /// </param>
    public void Start(string? connectAddress)
    {
        Stop();

        if (_statusClient == null || string.IsNullOrWhiteSpace(connectAddress))
        {
            StatusUpdated?.Invoke(null);
            return;
        }

        _loopCts = new CancellationTokenSource();
        _ = PollLoopAsync(connectAddress, _loopCts.Token);
    }

    public void Stop()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }

    private async Task PollLoopAsync(string connectAddress, CancellationToken cancel)
    {
        var statusClient = _statusClient;
        if (statusClient == null)
            return;

        while (!cancel.IsCancellationRequested)
        {
            ConnectingServerStatus? status = null;
            try
            {
                var info = await statusClient.FetchStatusAsync(null, null, connectAddress, cancel);
                if (info != null)
                {
                    status = new ConnectingServerStatus(
                        info.Value.Name,
                        info.Value.Players,
                        info.Value.SoftMaxPlayers,
                        info.Value.Map,
                        info.Value.Preset);
                }
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                status = null;
            }

            if (cancel.IsCancellationRequested)
                return;

            StatusUpdated?.Invoke(status);

            try
            {
                await Task.Delay(PollInterval, cancel);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}

public readonly record struct ConnectingServerStatus(
    string? Name,
    int? Players,
    int? SoftMaxPlayers,
    string? Map,
    string? Preset);
