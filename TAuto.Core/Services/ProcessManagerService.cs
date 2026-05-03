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
using TAuto.Shared.Ipc;

namespace TAuto.Core.Services;

/// <summary>
/// Manages Worker processes — spawn, monitor, restart, IPC.
/// Refactored to compose single-responsibility components.
/// </summary>
public class ProcessManagerService : IDisposable
{
    private readonly IProcessSpawner _processSpawner;
    private readonly INamedPipeRegistry _pipeRegistry;
    private readonly ICrashLoopProtector _crashProtector;
    private readonly IHeartbeatMonitor _heartbeatMonitor;
    private readonly ILogStreamer _logStreamer;
    private readonly ComputeTokenService _tokenService;
    private readonly ILogger<ProcessManagerService>? _logger;
    
    private readonly ConcurrentDictionary<string, IWorkerProcess> _workers = new();
    private readonly ConcurrentDictionary<string, string> _intentionalStops = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _workerLocks = new();
    private readonly ZombieWorkerReaper _zombieReaper;
    private readonly WorkerIpcListener _ipcListener;
    private bool _disposed;
    private volatile bool _isShuttingDown = false;
    private CancellationTokenSource _shutdownCts = new();
    private Timer? _memoryDiagnosticsTimer;

    public int HeartbeatTimeoutSeconds 
    { 
        get => _zombieReaper?.HeartbeatTimeoutSeconds ?? 15; 
        set { if (_zombieReaper != null) { _zombieReaper.HeartbeatTimeoutSeconds = value; } } 
    }

    public string WorkerExePath { get; set; } = string.Empty;
    public string VisionServerExePath { get; set; } = string.Empty;
    public int RestartDelayMs { get; set; } = AutomationDefaults.DefaultWorkerRestartDelayMs;
    public int ShutdownTimeoutMs { get; set; } = AutomationDefaults.DefaultWorkerShutdownTimeoutMs;
    public bool AutoRestart { get; set; } = true;
    public int StartCommandTimeoutMs { get; set; } = 10000;

    public event Action<string, WorkerLogEntry>? OnWorkerLog;
    public event Action<string, WorkerTraceEntry>? OnWorkerTrace;
    public event Action<string, string>? OnWorkerStatusChanged;
    public event Action<string, WorkerHeartbeat>? OnWorkerHeartbeat;
    public event Func<WorkerStartupArgs, string, Task>? OnAutoRestartRequested;

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

        OnWorkerLog += (workerId, log) => _logStreamer.WriteLog(workerId, log.Level ?? "INFO", log.Message ?? "");
        OnWorkerStatusChanged += (workerId, status) => _logStreamer.WriteStatus(workerId, status);
        OnWorkerHeartbeat += (workerId, hb) => _heartbeatMonitor.RecordHeartbeat(workerId, hb);

        WorkerExePath = ResolveExePath("AutoBot.Worker.exe", "worker");
        VisionServerExePath = ResolveExePath("AutoBot.VisionServer.exe", "vision");

        _zombieReaper = new ZombieWorkerReaper(_workers, _intentionalStops, _heartbeatMonitor, _processSpawner, _logger, (w, s) => OnWorkerStatusChanged?.Invoke(w, s));
        _ipcListener = new WorkerIpcListener(_tokenService, _logger, 
            (w, hb) => OnWorkerHeartbeat?.Invoke(w, hb),
            (w, l) => OnWorkerLog?.Invoke(w, l),
            (w, t) => OnWorkerTrace?.Invoke(w, t),
            (w, s) => OnWorkerStatusChanged?.Invoke(w, s));

        if (_logger != null)
        {
            _memoryDiagnosticsTimer = new Timer(_ => TAuto.Core.Diagnostics.MemoryDiagnostics.LogMemorySnapshot(_logger, "Manager"), null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        }
    }

    public IEnumerable<string> GetActiveWorkers() => _workers.Keys;

    public async Task<string> StartWorkerAsync(WorkerStartupArgs startupArgs, string botFolder, CancellationToken cancellationToken = default)
    {
        var workerId = string.IsNullOrEmpty(startupArgs.WorkerId) ? $"worker-{Random.Shared.Next(1000, 9999)}" : startupArgs.WorkerId;
        var workerLock = _workerLocks.GetOrAdd(workerId, _ => new SemaphoreSlim(1, 1));
        await workerLock.WaitAsync(cancellationToken);
        try
        {
            _intentionalStops.TryRemove(workerId, out _);
            startupArgs.WorkerId = workerId;

            if (_workers.TryGetValue(workerId, out var existingWorker) && existingWorker.Process is { HasExited: false })
            {
                if (existingWorker.StartupArgs.BaseDirectory == botFolder && existingWorker.StartupArgs.NativeExePath == startupArgs.NativeExePath)
                {
                    if (!string.IsNullOrEmpty(startupArgs.NativeExePath) && File.Exists(startupArgs.NativeExePath))
                    {
                        if (existingWorker.VisionProcess is { HasExited: false })
                        {
                            try { _processSpawner.KillProcess(existingWorker.VisionProcess); } catch { }
                            try { existingWorker.VisionProcess.Dispose(); } catch { }
                        }
                        startupArgs.VisionPipeName = $"AutoBot_Vision_{workerId}_{Guid.NewGuid():N}";
                        existingWorker.VisionProcess = StartVisionServerProcess(startupArgs.VisionPipeName, cancellationToken);
                        if (existingWorker is WorkerProcess wp) AttachVisionHandlers(workerId, wp);
                    }
                    if (existingWorker is WorkerProcess wpTrack) wpTrack.StartupArgs = startupArgs;
                    existingWorker.MessageChannel.Writer.TryWrite(IpcMessage.Create(IpcMessageTypes.Start, startupArgs).ToJson());
                    OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Starting);
                    return workerId;
                }
                else
                {
                    try { _processSpawner.KillProcess(existingWorker.Process); } catch { }
                    await RemoveWorkerAsync(workerId, "Replacing existing worker", existingWorker);
                }
            }

            if (!string.IsNullOrEmpty(startupArgs.BotDllPath) && File.Exists(startupArgs.BotDllPath))
            {
                try {
                    using var sha256 = System.Security.Cryptography.SHA256.Create();
                    using var fs = File.OpenRead(startupArgs.BotDllPath);
                    startupArgs.ExpectedPayloadHash = Convert.ToHexString(sha256.ComputeHash(fs));
                } catch { }
            }

            var pipeName = $"AutoBot_Worker_{workerId}_{Guid.NewGuid():N}";
            OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Starting);

            var pipeServer = _pipeRegistry.CreatePipeServer(pipeName);
            Process process;
            Process? visionProcess = null;
            try {
                var exePath = WorkerExePath;
                if (!string.IsNullOrEmpty(startupArgs.NativeExePath) && File.Exists(startupArgs.NativeExePath))
                {
                    exePath = startupArgs.NativeExePath;
                    startupArgs.VisionPipeName = $"AutoBot_Vision_{workerId}_{Guid.NewGuid():N}";
                    visionProcess = StartVisionServerProcess(startupArgs.VisionPipeName, cancellationToken);
                }
                process = _processSpawner.SpawnWorkerProcess(exePath, $"--pipe {pipeName} --id {workerId}", botFolder);
            }
            catch {
                if (visionProcess != null) { try { _processSpawner.KillProcess(visionProcess); } catch { } visionProcess.Dispose(); }
                pipeServer.Dispose(); throw;
            }

            var worker = new WorkerProcess { WorkerId = workerId, Process = process, VisionProcess = visionProcess, PipeServer = pipeServer, StartupArgs = startupArgs, Cts = new CancellationTokenSource(), StartTimeUtc = DateTime.UtcNow };
            _workers[workerId] = worker;

            if (visionProcess != null) AttachVisionHandlers(workerId, worker);
            AttachWorkerHandlers(workerId, worker);

            string? capturedBotDllPath = startupArgs.BotDllPath;
            worker.ExitedHandler = (s, e) => 
            { 
                worker.ExitHandlingTask = Task.Run(async () => await HandleWorkerExitAsync(workerId, worker, capturedBotDllPath)); 
            };
            process.Exited += worker.ExitedHandler;

            try {
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectCts.CancelAfter(AutomationDefaults.DefaultWorkerConnectTimeoutMs);
                void OnEarlyExit(object? sender, EventArgs e) => connectCts.Cancel();
                process.Exited += OnEarlyExit;
                try {
                    if (process.HasExited) connectCts.Cancel();
                    await _pipeRegistry.WaitForConnectionAsync(pipeServer, AutomationDefaults.DefaultWorkerConnectTimeoutMs, connectCts.Token);
                } finally { process.Exited -= OnEarlyExit; }
            }
            catch {
                _intentionalStops.TryAdd(workerId, "timeout"); _processSpawner.KillProcess(process); pipeServer.Dispose(); throw;
            }

            var noBomUtf8 = new UTF8Encoding(false);
            worker.Reader = new StreamReader(pipeServer, noBomUtf8);
            worker.Writer = new StreamWriter(pipeServer, noBomUtf8) { AutoFlush = true };

            using (var readyCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                readyCts.CancelAfter(AutomationDefaults.DefaultWorkerConnectTimeoutMs);
                while (!readyCts.IsCancellationRequested)
                {
                    var line = await worker.Reader.ReadLineAsync(readyCts.Token);
                    if (line == null) break;
                    if (IpcMessage.FromJson(line)?.Type == IpcMessageTypes.Ready) break;
                }
            }

            worker.WriterTask = Task.Run(() => ProcessWriterLoopAsync(worker), worker.Cts.Token);
            _ = Task.Run(() => _ipcListener.ListenAsync(worker), worker.Cts.Token);

            var requestId = Guid.NewGuid().ToString("N");
            var ackTask = _ipcListener.WaitForAckAsync(workerId, requestId, StartCommandTimeoutMs);
            worker.MessageChannel.Writer.TryWrite(IpcMessage.Create(IpcMessageTypes.Start, startupArgs, requestId).ToJson());

            if (!await ackTask) {
                _intentionalStops.TryAdd(workerId, "ack_timeout"); _processSpawner.KillProcess(process);
                throw new TimeoutException($"Worker '{workerId}' failed to ACK Start command.");
            }

            worker.IsInitialized = true;
            OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Running);

            if (_intentionalStops.ContainsKey(workerId)) {
                try { _processSpawner.KillProcess(worker.Process); } catch { }
                if (worker.VisionProcess != null) try { _processSpawner.KillProcess(worker.VisionProcess); } catch { }
                OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Stopped);
                _workers.TryRemove(workerId, out _);
            }

            return workerId;
        }
        finally { if (workerLock != null) try { workerLock.Release(); } catch { } }
    }

    public async Task StopWorkerAsync(string workerId)
    {
        var workerLock = _workerLocks.GetOrAdd(workerId, _ => new SemaphoreSlim(1, 1));
        bool locked = false;
        try { locked = await workerLock.WaitAsync(TimeSpan.FromMilliseconds(ShutdownTimeoutMs)); } catch { }

        try {
            if (!_workers.TryGetValue(workerId, out var worker)) return;
            _intentionalStops.TryAdd(workerId, "stopped");
            OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Stopping);
            
            try {
                worker.MessageChannel.Writer.TryWrite(IpcMessage.Create(IpcMessageTypes.Stop).ToJson());
                using var exitCts = new CancellationTokenSource(ShutdownTimeoutMs);
                await worker.Process.WaitForExitAsync(exitCts.Token);
            }
            catch {
                try { _processSpawner.KillProcess(worker.Process); if (worker.VisionProcess != null) _processSpawner.KillProcess(worker.VisionProcess); } catch { }
            }
            await RemoveWorkerAsync(workerId, "User requested stop", null, !locked);
        }
        finally { if (locked) try { workerLock.Release(); } catch { } }
    }

    public async Task StopAllWorkersAsync()
    {
        _isShuttingDown = true; _shutdownCts.Cancel();
        try {
            var ids = _workers.Keys.ToList();
            foreach (var id in ids) _intentionalStops.TryAdd(id, "stopped");
            await Task.WhenAll(ids.Select(StopWorkerAsync));
        }
        finally { _shutdownCts.Dispose(); _shutdownCts = new CancellationTokenSource(); _isShuttingDown = false; }
    }

    public Task<bool> SendMessageToWorkerAsync(string workerId, IpcMessage message)
    {
        if (!_workers.TryGetValue(workerId, out var worker)) return Task.FromResult(false);
        return Task.FromResult(worker.MessageChannel.Writer.TryWrite(message.ToJson()));
    }

    public List<(string Id, bool IsRunning, long MemoryBytes)> GetWorkerStatuses()
    {
        return _workers.Values.Select(w => {
            try { return (w.WorkerId, w.Process is { HasExited: false }, _heartbeatMonitor.GetLastHeartbeat(w.WorkerId)?.MemoryBytes ?? 0); }
            catch { return (w.WorkerId, false, 0L); }
        }).ToList();
    }

    public bool IsWorkerAlive(string workerId) => _workers.TryGetValue(workerId, out var w) && w.Process is { HasExited: false };

    public async Task<Dictionary<string, System.Text.Json.JsonElement>?> RequestVariablesAsync(string workerId, int timeoutMs = 5000)
    {
        var rid = Guid.NewGuid().ToString("N");
        var task = _ipcListener.WaitForVariablesAsync(workerId, rid, timeoutMs);
        if (await SendMessageToWorkerAsync(workerId, IpcMessage.Create(IpcMessageTypes.RequestVariables, rid))) return await task;
        return null;
    }

    private async Task RemoveWorkerAsync(string workerId, string reason = "Unknown", IWorkerProcess? expected = null, bool keepLock = false)
    {
        _logger?.LogInformation($"Removing worker '{workerId}'. Reason: {reason}");
        bool isCurrent = expected != null ? ((ICollection<KeyValuePair<string, IWorkerProcess>>)_workers).Remove(new KeyValuePair<string, IWorkerProcess>(workerId, expected)) : _workers.TryRemove(workerId, out expected);
        if (expected != null) {
            if (expected is WorkerProcess wp) {
                if (wp.Process != null) {
                    if (wp.ExitedHandler != null) try { wp.Process.Exited -= wp.ExitedHandler; } catch { }
                    if (wp.OutputHandler != null) try { wp.Process.OutputDataReceived -= wp.OutputHandler; } catch { }
                    if (wp.ErrorHandler != null) try { wp.Process.ErrorDataReceived -= wp.ErrorHandler; } catch { }
                }
                if (wp.VisionProcess != null) {
                    if (wp.VisionOutputHandler != null) try { wp.VisionProcess.OutputDataReceived -= wp.VisionOutputHandler; } catch { }
                    if (wp.VisionErrorHandler != null) try { wp.VisionProcess.ErrorDataReceived -= wp.VisionErrorHandler; } catch { }
                }
            }
            expected.Cts?.Cancel();
            expected.MessageChannel?.Writer.TryComplete();
            if (expected.WriterTask != null) 
            {
                try 
                { 
                    // P0-5: Increased timeout to ensure writer task has time to flush/close
                    await expected.WriterTask.WaitAsync(TimeSpan.FromSeconds(2)); 
                } 
                catch (TimeoutException)
                {
                    _logger?.LogWarning("RemoveWorkerAsync: WriterTask for '{WorkerId}' did not terminate within 2s.", workerId);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "RemoveWorkerAsync: Error waiting for WriterTask for '{WorkerId}'.", workerId);
                }
            }
            
            if (expected.ExitHandlingTask != null && Task.CurrentId != expected.ExitHandlingTask.Id) 
            {
                // We don't await ExitHandlingTask here because RemoveWorkerAsync is often called FROM it.
                // But we should ensure it's at least not completely ignored if called from StopWorkerAsync.
            }
            try { expected.Cts?.Dispose(); expected.Writer?.Dispose(); expected.Reader?.Dispose(); expected.PipeServer?.Dispose(); } catch { }
            try { if (expected.VisionProcess is { HasExited: false }) _processSpawner.KillProcess(expected.VisionProcess); } catch { }
            try { expected.VisionProcess?.Dispose(); expected.Process?.Dispose(); } catch { }
        }
        if (isCurrent) { 
            _heartbeatMonitor.Clear(workerId); 
            _logStreamer.CloseWriter(workerId); 
            if (!keepLock) _workerLocks.TryRemove(workerId, out _); 
        }
    }

    private Process StartVisionServerProcess(string pipe, CancellationToken ct) {
        if (!File.Exists(VisionServerExePath)) throw new FileNotFoundException();
        return _processSpawner.SpawnWorkerProcess(VisionServerExePath, $"--pipe {pipe}", Path.GetDirectoryName(VisionServerExePath) ?? AppDomain.CurrentDomain.BaseDirectory);
    }

    private void AttachWorkerHandlers(string id, WorkerProcess w) {
        w.OutputHandler = (_, e) => { if (e.Data != null) OnWorkerLog?.Invoke(id, new WorkerLogEntry { WorkerId = id, Level = "INFO", Message = $"[OUT] {e.Data}" }); };
        w.ErrorHandler = (_, e) => { if (e.Data != null) OnWorkerLog?.Invoke(id, new WorkerLogEntry { WorkerId = id, Level = "ERROR", Message = $"[ERR] {id}: {e.Data}" }); };
        w.Process.OutputDataReceived += w.OutputHandler; w.Process.ErrorDataReceived += w.ErrorHandler;
        w.Process.BeginOutputReadLine(); w.Process.BeginErrorReadLine();
    }

    private void AttachVisionHandlers(string id, WorkerProcess w) {
        if (w.VisionProcess == null) return;
        w.VisionOutputHandler = (_, e) => { if (e.Data != null) OnWorkerLog?.Invoke(id, new WorkerLogEntry { WorkerId = id, Level = "INFO", Message = $"[VIS-OUT] {e.Data}" }); };
        w.VisionErrorHandler = (_, e) => { if (e.Data != null) OnWorkerLog?.Invoke(id, new WorkerLogEntry { WorkerId = id, Level = "ERROR", Message = $"[VIS-ERR] {e.Data}" }); };
        w.VisionProcess.OutputDataReceived += w.VisionOutputHandler; w.VisionProcess.ErrorDataReceived += w.VisionErrorHandler;
        w.VisionProcess.BeginOutputReadLine(); w.VisionProcess.BeginErrorReadLine();
    }

    private async Task HandleWorkerExitAsync(string id, WorkerProcess w, string? dll) {
        var workerLock = _workerLocks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        bool locked = false;
        try {
            var code = w.Process.ExitCode;
            OnWorkerLog?.Invoke(id, new WorkerLogEntry { WorkerId = id, Level = "ERROR", Message = $"Exited with {code}" });
            _tokenService.ReleaseAllForWorker(id);
            bool stop = _intentionalStops.TryRemove(id, out var reason);
            string status = code == 0 ? WorkerStates.Stopped : (code == -2 ? WorkerStates.StartTimeout : (code == -3 ? WorkerStates.HardwareMissing : WorkerStates.Crashed));
            if (stop && reason == "reaped") status = WorkerStates.ZombieReaped;
            OnWorkerStatusChanged?.Invoke(id, status);
            bool restart = AutoRestart && !stop && status != WorkerStates.Stopped && status != WorkerStates.StartTimeout && !_disposed && !_isShuttingDown;
            
            if (restart) {
                // If restarting, we acquire the lock to prevent manual starts during the delay
                try { locked = await workerLock.WaitAsync(TimeSpan.FromSeconds(5)); } catch { }
                await RemoveWorkerAsync(id, "Worker process exited (restarting)", w, true);
                if (_crashProtector.RegisterCrashAndCheckIfLooping(id)) { OnWorkerStatusChanged?.Invoke(id, WorkerStates.CrashLoopStopped); return; }
                OnWorkerStatusChanged?.Invoke(id, WorkerStates.Restarting);
                try { await Task.Delay(RestartDelayMs, _shutdownCts.Token); } catch { return; }
                if (!_disposed && !_isShuttingDown) {
                    if (OnAutoRestartRequested != null) await OnAutoRestartRequested(w.StartupArgs, Path.GetDirectoryName(dll) ?? "");
                    else await StartWorkerAsync(w.StartupArgs, Path.GetDirectoryName(dll) ?? "");
                }
            }
            else {
                await RemoveWorkerAsync(id, "Worker process exited", w, false);
            }
        } catch { OnWorkerStatusChanged?.Invoke(id, WorkerStates.HandlerError); }
        finally { if (locked) try { workerLock.Release(); } catch { } }
    }

    private async Task ProcessWriterLoopAsync(IWorkerProcess w) {
        try {
            while (await w.MessageChannel.Reader.WaitToReadAsync(w.Cts.Token))
                while (w.MessageChannel.Reader.TryRead(out var m))
                    try { await w.Writer.WriteLineAsync(m).WaitAsync(TimeSpan.FromSeconds(15), w.Cts.Token); } catch { _ = RemoveWorkerAsync(w.WorkerId, "Pipe write timeout (15s)", w); return; }
        } catch { _ = RemoveWorkerAsync(w.WorkerId, "Writer loop exception"); }
    }

    public void MarkIntentionalStop(string id) => _intentionalStops.TryAdd(id, "cleared");
    public void ClearCrashHistory(string id) => _crashProtector.ClearHistory(id);

    public void Dispose() {
        if (_disposed) return;
        _disposed = true;
        try { _shutdownCts.Cancel(); } catch { }
        _shutdownCts.Dispose();
        _zombieReaper?.Dispose();
        _memoryDiagnosticsTimer?.Dispose();
        _processSpawner.TerminateAll();
        if (_processSpawner is IDisposable d) d.Dispose();
        _tokenService.Dispose();
        _logStreamer.Dispose();
        foreach (var w in _workers.Values) { w.Cts?.Cancel(); w.Writer?.Dispose(); w.Reader?.Dispose(); w.PipeServer?.Dispose(); }
        _workers.Clear();
    }

    private string ResolveExePath(string exe, string sub) {
        var bd = AppDomain.CurrentDomain.BaseDirectory;
        var p1 = Path.Combine(bd, "bin", sub, exe);
        if (File.Exists(p1)) return p1;
        var p2 = Path.Combine(bd, exe);
        return p2;
    }
}
