using System;
using System.IO;
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
        }
    }
}
