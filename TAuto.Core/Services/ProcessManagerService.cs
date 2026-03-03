using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AutoBot.Shared.Ipc;

namespace TAuto.Core.Services;

/// <summary>
/// Manages Worker processes — spawn, monitor, restart, IPC.
/// Refactored to compose single-responsibility components.
/// </summary>
public class ProcessManagerService : IDisposable
{
    private static readonly Random _random = new();
    
    private readonly IProcessSpawner _processSpawner;
    private readonly INamedPipeRegistry _pipeRegistry;
    private readonly ICrashLoopProtector _crashProtector;
    private readonly IHeartbeatMonitor _heartbeatMonitor;
    private readonly ILogStreamer _logStreamer;
    private readonly ComputeTokenService _tokenService;
    private readonly ILogger<ProcessManagerService>? _logger;
    
    private readonly ConcurrentDictionary<string, WorkerProcess> _workers = new();
    private readonly ConcurrentDictionary<string, string> _intentionalStops = new();
    private bool _disposed;
    private Timer? _heartbeatReaperTimer;

    /// <summary>Max seconds without heartbeat before a worker is considered zombie and killed.</summary>
    public int HeartbeatTimeoutSeconds { get; set; } = 15;

    /// <summary>Path to the Worker executable.</summary>
    public string WorkerExePath { get; set; }

    /// <summary>Time to wait before restarting a crashed Worker.</summary>
    public int RestartDelayMs { get; set; } = AutomationDefaults.DefaultWorkerRestartDelayMs;

    /// <summary>Timeout for graceful shutdown before hard-kill.</summary>
    public int ShutdownTimeoutMs { get; set; } = AutomationDefaults.DefaultWorkerShutdownTimeoutMs;

    /// <summary>Enable auto-restart of crashed Workers.</summary>
    public bool AutoRestart { get; set; } = true;

    // Events
    public event Action<string, WorkerLogEntry>? OnWorkerLog;
    public event Action<string, string>? OnWorkerStatusChanged;
    public event Action<string, WorkerHeartbeat>? OnWorkerHeartbeat;

    public ProcessManagerService(
        IProcessSpawner? processSpawner = null,
        INamedPipeRegistry? pipeRegistry = null,
        ICrashLoopProtector? crashProtector = null,
        IHeartbeatMonitor? heartbeatMonitor = null,
        ILogStreamer? logStreamer = null,
        ComputeTokenService? tokenService = null,
        ILogger<ProcessManagerService>? logger = null)
    {
        _processSpawner = processSpawner ?? new DefaultProcessSpawner();
        _pipeRegistry = pipeRegistry ?? new DefaultNamedPipeRegistry();
        _crashProtector = crashProtector ?? new DefaultCrashLoopProtector();
        _heartbeatMonitor = heartbeatMonitor ?? new DefaultHeartbeatMonitor();
        _logStreamer = logStreamer ?? new WorkerLogService();
        _tokenService = tokenService ?? new ComputeTokenService();
        _logger = logger;

        // Wire structured per-worker logging
        OnWorkerLog += (workerId, log) =>
            _logStreamer.WriteLog(workerId, log.Level ?? "INFO", log.Message ?? "");
        OnWorkerStatusChanged += (workerId, status) =>
            _logStreamer.WriteStatus(workerId, status);
        OnWorkerHeartbeat += (workerId, hb) =>
            _heartbeatMonitor.RecordHeartbeat(workerId, hb);

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        WorkerExePath = Path.Combine(baseDir, "AutoBot.Worker.exe");

        // FIX-1 (Audit): Heartbeat reaper — kills zombie workers with no heartbeat
        _heartbeatReaperTimer = new Timer(ReapZombieWorkers, null,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));
    }

    private void ReapZombieWorkers(object? state)
    {
        if (_disposed) return;
        foreach (var kvp in _workers)
        {
            try
            {
                var worker = kvp.Value;
                if (worker.Process.HasExited) continue;

                var lastHb = _heartbeatMonitor.GetLastHeartbeatTime(worker.WorkerId);
                if (lastHb == null)
                {
                    // Audit FIX-1: Grace period for initialization before first heartbeat
                    var age = (DateTime.UtcNow - worker.StartTimeUtc).TotalSeconds;
                    if (age > 30)
                    {
                        _logger?.LogWarning("HEARTBEAT REAPER: Worker '{WorkerId}' failed to send initial heartbeat within 30s. Killing.", worker.WorkerId);
                        _intentionalStops.TryAdd(worker.WorkerId, "reaped");
                        _processSpawner.KillProcess(worker.Process);
                        OnWorkerStatusChanged?.Invoke(worker.WorkerId, WorkerStates.ZombieReaped);
                    }
                    continue;
                }

                var elapsed = (DateTime.UtcNow - lastHb.Value).TotalSeconds;
                if (elapsed > HeartbeatTimeoutSeconds)
                {
                    _logger?.LogWarning(
                        "HEARTBEAT REAPER: Worker '{WorkerId}' has not sent a heartbeat in {Elapsed:F0}s. Killing.",
                        worker.WorkerId, elapsed);
                    _intentionalStops.TryAdd(worker.WorkerId, "reaped");
                    _processSpawner.KillProcess(worker.Process);
                    OnWorkerStatusChanged?.Invoke(worker.WorkerId, WorkerStates.ZombieReaped);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Heartbeat reaper error for worker '{WorkerId}': {Error}", kvp.Key, ex.Message);
            }
        }
    }

    public async Task<string> StartWorkerAsync(WorkerStartupArgs startupArgs, string botFolder, CancellationToken cancellationToken = default)
    {
        var workerId = string.IsNullOrEmpty(startupArgs.WorkerId) 
            ? $"worker-{_random.Next(1000, 9999)}" 
            : startupArgs.WorkerId;
            
        startupArgs.WorkerId = workerId;
        // FIX-3 (Audit): Unique pipe name per session to avoid OS handle collisions on restart
        var pipeName = $"AutoBot_Worker_{workerId}_{Guid.NewGuid():N}";

        OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Starting);

        // 1. Create Named Pipe Server
        NamedPipeServerStream pipeServer;
        try
        {
            pipeServer = _pipeRegistry.CreatePipeServer(pipeName);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Pipe creation failed for '{workerId}': {ex.Message}");
            OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.PipeError);
            throw;
        }

        // 2. Spawn Worker Process
        Process process;
        try
        {
            process = _processSpawner.SpawnWorkerProcess(WorkerExePath, $"--pipe {pipeName} --id {workerId}", botFolder);
        }
        catch (Exception ex)
        {
            pipeServer.Dispose();
            _logger?.LogError($"Error spawning worker: {ex.Message}");
            throw;
        }

        var worker = new WorkerProcess
        {
            WorkerId = workerId,
            Process = process,
            PipeServer = pipeServer,
            StartupArgs = startupArgs,
            Cts = new CancellationTokenSource(),
            StartTimeUtc = DateTime.UtcNow
        };
        _workers[workerId] = worker;

        // FIX-3: Attach Process Monitors Immediately
        process.Exited += async (_, _) =>
        {
            try
            {
                var exitCode = process.ExitCode;
                _logger?.LogInformation($"Worker '{workerId}' exited with code {exitCode}");

                _tokenService.ReleaseAllForWorker(workerId);
                // FIX-1 (Audit): exitCode -1 IS now a crash (unhandled exception).
                // Only -2 (deliberate startup timeout) is treated as non-crash.
                // FIX-4 (Audit): exitCode -3 = hardware unavailable — crash but NO auto-restart.
                bool isCrash = exitCode != 0 && exitCode != -2;
                bool isHardwareMissing = exitCode == -3;
                
                if (_intentionalStops.TryRemove(workerId, out var stopReason))
                {
                    isCrash = false;
                    isHardwareMissing = false;
                    // Audit FIX-6: Do not clear history on intentional stop to avoid bypassing crash loop memory
                    
                    // Audit FIX-3: Don't overwrite ZombieReaped status with Stopped
                    if (stopReason == "reaped")
                    {
                        await RemoveWorkerAsync(workerId);
                        return;
                    }
                }

                OnWorkerStatusChanged?.Invoke(workerId, isCrash ? (isHardwareMissing ? WorkerStates.HardwareMissing : WorkerStates.Crashed) : WorkerStates.Stopped);
                await RemoveWorkerAsync(workerId);

                if (AutoRestart && isCrash && !isHardwareMissing && !_disposed)
                {
                    bool isLooping = _crashProtector.RegisterCrashAndCheckIfLooping(workerId);
                    if (isLooping)
                    {
                        _logger?.LogError($"CRASH LOOP DETECTED for '{workerId}'. Stopping auto-restart.");
                        OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.CrashLoopStopped);
                        return;
                    }

                    OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Restarting);
                    await Task.Delay(RestartDelayMs);

                    if (!_disposed && AutoRestart && !_intentionalStops.ContainsKey(workerId))
                    {
                        try
                        {
                            string botDir = Path.GetDirectoryName(startupArgs.BotDllPath)!;
                            await StartWorkerAsync(startupArgs, botDir);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError($"Failed to restart Worker '{workerId}': {ex.Message}");
                            OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.RestartFailed);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogCritical($"CRITICAL: Worker '{workerId}' Exited handler failed: {ex.Message}");
                OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.HandlerError);
            }
        };

        // 3. Wait for Worker to connect
        try
        {
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(AutomationDefaults.DefaultWorkerConnectTimeoutMs);
            await _pipeRegistry.WaitForConnectionAsync(pipeServer, AutomationDefaults.DefaultWorkerConnectTimeoutMs, connectCts.Token);
        }
        catch (Exception ex)
        {
            _intentionalStops.TryAdd(workerId, "timeout");
            _processSpawner.KillProcess(process);
            pipeServer.Dispose();
            throw new TimeoutException($"Worker '{workerId}' failed to connect: {ex.Message}");
        }

        // 4. Create WorkerProcess tracking object
        var noBomUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var reader = new StreamReader(pipeServer, noBomUtf8, detectEncodingFromByteOrderMarks: false);
        var writer = new StreamWriter(pipeServer, noBomUtf8) { AutoFlush = true };

        worker.Reader = reader;
        worker.Writer = writer;

        // 5. Wait for Ready signal
        {
            using var readyCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readyCts.CancelAfter(AutomationDefaults.DefaultWorkerConnectTimeoutMs);
            try
            {
                var readyLine = await worker.Reader.ReadLineAsync(readyCts.Token);
                var readyMsg = IpcMessage.FromJson(readyLine ?? "");

                if (readyMsg?.Type != IpcMessageTypes.Ready)
                {
                    _logger?.LogWarning($"Worker '{workerId}' didn't send Ready. Got: {readyMsg?.Type}");
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _intentionalStops.TryAdd(workerId, "timeout");
                OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.TimeoutReady);
                _processSpawner.KillProcess(process);
                await RemoveWorkerAsync(workerId);
                
                throw new TimeoutException($"Worker '{workerId}' failed to send Ready within 10s");
            }
        }

        // 6. Send start command
        var startMsg = IpcMessage.Create(IpcMessageTypes.Start, startupArgs);
        await worker.Writer.WriteLineAsync(startMsg.ToJson());

        OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Running);

        // Start background message listener
        _ = Task.Run(() => ListenToWorkerAsync(worker), worker.Cts.Token);



        return workerId;
    }

    public async Task StopWorkerAsync(string workerId)
    {
        _workers.TryGetValue(workerId, out WorkerProcess? worker);
        if (worker == null) return;

        _intentionalStops.TryAdd(workerId, "stopped");
        OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Stopping);

        try
        {
            var stopMsg = IpcMessage.Create(IpcMessageTypes.Stop);
            await worker.Writer.WriteLineAsync(stopMsg.ToJson());

            var exited = worker.Process.WaitForExit(ShutdownTimeoutMs);
            if (!exited)
            {
                _logger?.LogWarning($"Worker '{workerId}' didn't stop gracefully. Killing.");
                _processSpawner.KillProcess(worker.Process);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error stopping Worker '{workerId}': {ex.Message}");
            _processSpawner.KillProcess(worker.Process);
        }
        finally
        {
            await RemoveWorkerAsync(workerId);
        }
    }

    public async Task StopAllWorkersAsync()
    {
        var wasAutoRestart = AutoRestart;
        AutoRestart = false;
        try
        {
            var workerIds = _workers.Keys.ToList();
            foreach (var id in workerIds)
            {
                _intentionalStops.TryAdd(id, "stopped");
            }

            var stopTasks = workerIds.Select(id => StopWorkerAsync(id));
            await Task.WhenAll(stopTasks);
        }
        finally
        {
            AutoRestart = wasAutoRestart;
        }
    }

    public List<(string Id, bool IsRunning, long MemoryBytes)> GetWorkerStatuses()
    {
        return _workers.Values.Select(w => (
            w.WorkerId,
            !w.Process.HasExited,
            _heartbeatMonitor.GetLastHeartbeat(w.WorkerId)?.MemoryBytes ?? 0
        )).ToList();
    }

    private async Task ListenToWorkerAsync(WorkerProcess worker)
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
                            OnWorkerHeartbeat?.Invoke(worker.WorkerId, hb);
                        }
                        break;

                    case IpcMessageTypes.Log:
                        var log = msg.GetPayload<WorkerLogEntry>();
                        if (log != null)
                        {
                            OnWorkerLog?.Invoke(worker.WorkerId, log);
                        }
                        break;

                    case IpcMessageTypes.Exiting:
                        _logger?.LogInformation($"Worker '{worker.WorkerId}' sent exit notification");
                        break;

                    case IpcMessageTypes.StatusUpdate:
                        var statusStr = msg.GetPayload<string>();
                        if (!string.IsNullOrEmpty(statusStr))
                        {
                            OnWorkerStatusChanged?.Invoke(worker.WorkerId, statusStr);
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

    private Task RemoveWorkerAsync(string workerId)
    {
        if (_workers.TryRemove(workerId, out var worker))
        {
            worker.Cts.Cancel();
            try { worker.Writer?.Dispose(); } catch { }
            try { worker.Reader?.Dispose(); } catch { }
            try { worker.PipeServer?.Dispose(); } catch { }
            
            _heartbeatMonitor.Clear(workerId);
            _logStreamer.CloseWriter(workerId);
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _heartbeatReaperTimer?.Dispose();
        _processSpawner.TerminateAll();
        if (_processSpawner is IDisposable dispSpawner) dispSpawner.Dispose();
        
        _tokenService.Dispose();
        _logStreamer.Dispose();

        foreach (var worker in _workers.Values)
        {
            worker.Cts?.Cancel();
            worker.Writer?.Dispose();
            worker.Reader?.Dispose();
            worker.PipeServer?.Dispose();
        }
        _workers.Clear();
    }

    private class WorkerProcess
    {
        public string WorkerId { get; set; } = string.Empty;
        public Process Process { get; set; } = null!;
        public NamedPipeServerStream PipeServer { get; set; } = null!;
        public StreamReader Reader { get; set; } = null!;
        public StreamWriter Writer { get; set; } = null!;
        public WorkerStartupArgs StartupArgs { get; set; } = null!;
        public CancellationTokenSource Cts { get; set; } = null!;
        public DateTime StartTimeUtc { get; set; }
    }
}
