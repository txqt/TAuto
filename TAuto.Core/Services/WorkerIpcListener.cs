using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TAuto.Shared.Ipc;

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
    private readonly Action<string, WorkerTraceEntry>? _onWorkerTrace;
    private readonly Action<string, string>? _onWorkerStatusChanged;

    // Pending variable snapshot requests keyed by RequestId
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Dictionary<string, JsonElement>>> _pendingVarRequests = new();
    
    // Pending ACK requests keyed by RequestId
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingAcks = new();


    public WorkerIpcListener(
        ComputeTokenService tokenService,
        ILogger? logger,
        Action<string, WorkerHeartbeat>? onWorkerHeartbeat,
        Action<string, WorkerLogEntry>? onWorkerLog,
        Action<string, WorkerTraceEntry>? onWorkerTrace,
        Action<string, string>? onWorkerStatusChanged)
    {
        _tokenService = tokenService;
        _logger = logger;
        _onWorkerHeartbeat = onWorkerHeartbeat;
        _onWorkerLog = onWorkerLog;
        _onWorkerTrace = onWorkerTrace;
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
                            if (string.IsNullOrEmpty(log.WorkerId))
                                log.WorkerId = worker.WorkerId;
                            _onWorkerLog?.Invoke(worker.WorkerId, log);
                        }
                        break;
                    case IpcMessageTypes.Trace:
                        var trace = msg.GetPayload<WorkerTraceEntry>();
                        if (trace != null)
                        {
                            _onWorkerTrace?.Invoke(worker.WorkerId, trace);
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
                        worker.MessageChannel.Writer.TryWrite(response.ToJson());
                        break;

                    case IpcMessageTypes.ReleaseToken:
                        _tokenService.Release(worker.WorkerId);
                        break;

                    case IpcMessageTypes.VariablesSnapshot:
                        var snapshot = msg.GetPayload<Dictionary<string, JsonElement>>();
                        var rid = msg.RequestId;
                        if (!string.IsNullOrEmpty(rid) && _pendingVarRequests.TryRemove(rid, out var tcs))
                        {
                            tcs.TrySetResult(snapshot ?? new Dictionary<string, JsonElement>());
                        }
                        break;
                    case IpcMessageTypes.Ack:
                        var ackRid = msg.RequestId;
                        if (!string.IsNullOrEmpty(ackRid) && _pendingAcks.TryRemove(ackRid, out var ackTcs))
                        {
                            ackTcs.TrySetResult(true);
                        }
                        break;
                    case IpcMessageTypes.Nack:
                        var nackRid = msg.RequestId;
                        if (!string.IsNullOrEmpty(nackRid) && _pendingAcks.TryRemove(nackRid, out var nackTcs))
                        {
                            nackTcs.TrySetResult(false);
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
            // Cancel any pending variable requests (Cleanup of RequestId-based dict is handled by timeouts)
        }
    }

    /// <summary>
    /// Requests variables from a worker and waits for the snapshot reply.
    /// The actual IPC send must be done by the caller; this only registers the wait.
    /// </summary>
    public async Task<Dictionary<string, JsonElement>?> WaitForVariablesAsync(string workerId, string requestId, int timeoutMs = 5000)
    {
        var tcs = new TaskCompletionSource<Dictionary<string, JsonElement>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingVarRequests[requestId] = tcs;

        using var cts = new CancellationTokenSource(timeoutMs);
        using var reg = cts.Token.Register(() =>
        {
            if (_pendingVarRequests.TryRemove(requestId, out var removed))
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

    public async Task<bool> WaitForAckAsync(string workerId, string requestId, int timeoutMs = 5000)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingAcks[requestId] = tcs;

        using var cts = new CancellationTokenSource(timeoutMs);
        using var reg = cts.Token.Register(() =>
        {
            if (_pendingAcks.TryRemove(requestId, out var removed))
                removed.TrySetCanceled();
        });

        try
        {
            return await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("ACK for worker '{WorkerId}' (Req: {RequestId}) timed out after {Timeout}ms.", workerId, requestId, timeoutMs);
            return false;
        }
    }
}
