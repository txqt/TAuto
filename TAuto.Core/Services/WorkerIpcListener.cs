using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AutoBot.Shared.Ipc;

namespace TAuto.Core.Services;

/// <summary>
/// Handles IPC (Inter-Process Communication) message listening for a connected worker.
/// </summary>
public class WorkerIpcListener
{
    private readonly ComputeTokenService _tokenService;
    private readonly ILogger? _logger;
    private readonly Action<string, WorkerHeartbeat>? _onWorkerHeartbeat;
    private readonly Action<string, WorkerLogEntry>? _onWorkerLog;
    private readonly Action<string, string>? _onWorkerStatusChanged;

    // Pending variable snapshot requests keyed by workerId
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Dictionary<string, JsonElement>>> _pendingVarRequests = new();

    public WorkerIpcListener(
        ComputeTokenService tokenService,
        ILogger? logger,
        Action<string, WorkerHeartbeat>? onWorkerHeartbeat,
        Action<string, WorkerLogEntry>? onWorkerLog,
        Action<string, string>? onWorkerStatusChanged)
    {
        _tokenService = tokenService;
        _logger = logger;
        _onWorkerHeartbeat = onWorkerHeartbeat;
        _onWorkerLog = onWorkerLog;
        _onWorkerStatusChanged = onWorkerStatusChanged;
    }

    public async Task ListenAsync(IWorkerProcess worker)
    {
        try
        {
            while (!worker.Cts.IsCancellationRequested && worker.PipeServer.IsConnected)
            {
                var line = await worker.Reader.ReadLineAsync(worker.Cts.Token);
                if (line == null) break;

                var msg = IpcMessage.FromJson(line);
                if (msg == null) continue;

                switch (msg.Type)
                {
                    case IpcMessageTypes.Heartbeat:
                        var hb = msg.GetPayload<WorkerHeartbeat>();
                        if (hb != null)
                        {
                            _onWorkerHeartbeat?.Invoke(worker.WorkerId, hb);
                        }
                        break;

                    case IpcMessageTypes.Log:
                        var log = msg.GetPayload<WorkerLogEntry>();
                        if (log != null)
                        {
                            _onWorkerLog?.Invoke(worker.WorkerId, log);
                        }
                        break;

                    case IpcMessageTypes.Exiting:
                        _logger?.LogInformation($"Worker '{worker.WorkerId}' sent exit notification");
                        break;

                    case IpcMessageTypes.StatusUpdate:
                        var statusStr = msg.GetPayload<string>();
                        if (!string.IsNullOrEmpty(statusStr))
                        {
                            _onWorkerStatusChanged?.Invoke(worker.WorkerId, statusStr);
                        }
                        break;

                    case IpcMessageTypes.RequestToken:
                        var granted = await _tokenService.TryAcquireAsync(worker.WorkerId, 5000);
                        var response = IpcMessage.Create(
                            granted ? IpcMessageTypes.TokenGranted : IpcMessageTypes.TokenDenied);
                        await worker.Writer.WriteLineAsync(response.ToJson());
                        break;

                    case IpcMessageTypes.ReleaseToken:
                        _tokenService.Release(worker.WorkerId);
                        break;

                    case IpcMessageTypes.VariablesSnapshot:
                        var snapshot = msg.GetPayload<Dictionary<string, JsonElement>>();
                        if (_pendingVarRequests.TryRemove(worker.WorkerId, out var tcs))
                        {
                            tcs.TrySetResult(snapshot ?? new Dictionary<string, JsonElement>());
                        }
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (Exception ex)
        {
            _logger?.LogError($"Listener error for '{worker.WorkerId}': {ex.Message}");
        }
        finally
        {
            _tokenService.ReleaseAllForWorker(worker.WorkerId);
            // Cancel any pending variable request for this worker
            if (_pendingVarRequests.TryRemove(worker.WorkerId, out var pendingTcs))
                pendingTcs.TrySetCanceled();
        }
    }

    /// <summary>
    /// Requests variables from a worker and waits for the snapshot reply.
    /// The actual IPC send must be done by the caller; this only registers the wait.
    /// </summary>
    public async Task<Dictionary<string, JsonElement>?> WaitForVariablesAsync(string workerId, int timeoutMs = 5000)
    {
        var tcs = new TaskCompletionSource<Dictionary<string, JsonElement>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingVarRequests[workerId] = tcs;

        using var cts = new CancellationTokenSource(timeoutMs);
        using var reg = cts.Token.Register(() =>
        {
            if (_pendingVarRequests.TryRemove(workerId, out var removed))
                removed.TrySetCanceled();
        });

        try
        {
            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Variable request for worker '{WorkerId}' timed out after {Timeout}ms.", workerId, timeoutMs);
            return null;
        }
    }
}
