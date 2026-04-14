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
    
    
    private readonly IProcessSpawner _processSpawner;
    private readonly INamedPipeRegistry _pipeRegistry;
    private readonly ICrashLoopProtector _crashProtector;
    private readonly IHeartbeatMonitor _heartbeatMonitor;
    private readonly ILogStreamer _logStreamer;
    private readonly ComputeTokenService _tokenService;
    private readonly ILogger<ProcessManagerService>? _logger;
    
    private readonly ConcurrentDictionary<string, IWorkerProcess> _workers = new();
    private readonly ConcurrentDictionary<string, string> _intentionalStops = new();
    private readonly ZombieWorkerReaper _zombieReaper;
    private readonly WorkerIpcListener _ipcListener;
    private bool _disposed;
    private volatile bool _isShuttingDown = false;
    private CancellationTokenSource _shutdownCts = new(); // FIX C-3: cancels pending restart delays during StopAll
    private Timer? _memoryDiagnosticsTimer;

    /// <summary>Max seconds without heartbeat before a worker is considered zombie and killed.</summary>
    public int HeartbeatTimeoutSeconds 
    { 
        get => _zombieReaper?.HeartbeatTimeoutSeconds ?? 15; 
        set { if (_zombieReaper != null) { _zombieReaper.HeartbeatTimeoutSeconds = value; } } 
    }

    /// <summary>Path to the Worker executable.</summary>
    public string WorkerExePath { get; set; }
    /// <summary>Path to the VisionServer executable used by native bots.</summary>
    public string VisionServerExePath { get; set; }

    /// <summary>Time to wait before restarting a crashed Worker.</summary>
    public int RestartDelayMs { get; set; } = AutomationDefaults.DefaultWorkerRestartDelayMs;

    /// <summary>Timeout for graceful shutdown before hard-kill.</summary>
    public int ShutdownTimeoutMs { get; set; } = AutomationDefaults.DefaultWorkerShutdownTimeoutMs;

    /// <summary>Enable auto-restart of crashed Workers.</summary>
    public bool AutoRestart { get; set; } = true;

    // Events
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

        // Wire structured per-worker logging
        OnWorkerLog += (workerId, log) =>
            _logStreamer.WriteLog(workerId, log.Level ?? "INFO", log.Message ?? "");
        OnWorkerStatusChanged += (workerId, status) =>
            _logStreamer.WriteStatus(workerId, status);
        OnWorkerHeartbeat += (workerId, hb) =>
            _heartbeatMonitor.RecordHeartbeat(workerId, hb);

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        WorkerExePath = Path.Combine(baseDir, "AutoBot.Worker.exe");
        VisionServerExePath = Path.Combine(baseDir, "AutoBot.VisionServer.exe");

        // FIX-1 (Audit): Heartbeat reaper — kills zombie workers with no heartbeat
        _zombieReaper = new ZombieWorkerReaper(_workers, _intentionalStops, _heartbeatMonitor, _processSpawner, _logger, (w, s) => OnWorkerStatusChanged?.Invoke(w, s));
        _ipcListener = new WorkerIpcListener(_tokenService, _logger, 
            (w, hb) => OnWorkerHeartbeat?.Invoke(w, hb),
            (w, l) => OnWorkerLog?.Invoke(w, l),
            (w, t) => OnWorkerTrace?.Invoke(w, t),
            (w, s) => OnWorkerStatusChanged?.Invoke(w, s));

        // Periodic memory diagnostic logging (Phase G)
        if (_logger != null)
        {
            _memoryDiagnosticsTimer = new Timer(_ => 
                TAuto.Core.Diagnostics.MemoryDiagnostics.LogMemorySnapshot(_logger, "Manager"), 
                null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        }
    }



    public async Task<string> StartWorkerAsync(WorkerStartupArgs startupArgs, string botFolder, CancellationToken cancellationToken = default)
    {
        var workerId = string.IsNullOrEmpty(startupArgs.WorkerId) 
            ? $"worker-{Random.Shared.Next(1000, 9999)}" 
            : startupArgs.WorkerId;
            
        // Clean up any stale intentional stops from previous sessions for this workerId
        _intentionalStops.TryRemove(workerId, out _);
        
        startupArgs.WorkerId = workerId;

        // P3 (Audit): Compute expected payload checksum before sending args
        if (!string.IsNullOrEmpty(startupArgs.BotDllPath) && File.Exists(startupArgs.BotDllPath))
        {
            try
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                using var fs = File.OpenRead(startupArgs.BotDllPath);
                startupArgs.ExpectedPayloadHash = Convert.ToHexString(sha256.ComputeHash(fs));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Failed to compute payload hash for '{startupArgs.BotDllPath}': {ex.Message}");
            }
        }
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
        Process? visionProcess = null;
        try
        {
            var exePath = WorkerExePath;
            var isNativeBot = !string.IsNullOrEmpty(startupArgs.NativeExePath) &&
                              startupArgs.NativeExePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                              File.Exists(startupArgs.NativeExePath);
            
            // Support starting a Native AOT bot directly
            if (isNativeBot)
            {
                exePath = startupArgs.NativeExePath;
                _logger?.LogInformation($"Native AOT Executable detected: Overriding worker EXE to '{exePath}'");

                startupArgs.VisionPipeName = $"AutoBot_Vision_{workerId}_{Guid.NewGuid():N}";
                visionProcess = StartVisionServerProcess(startupArgs.VisionPipeName);
                visionProcess.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        OnWorkerLog?.Invoke(workerId, new WorkerLogEntry
                        {
                            WorkerId = workerId,
                            Level = "INFO",
                            Message = $"[VISION-OUT] {e.Data}"
                        });
                    }
                };
                visionProcess.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        OnWorkerLog?.Invoke(workerId, new WorkerLogEntry
                        {
                            WorkerId = workerId,
                            Level = "ERROR",
                            Message = $"[VISION-ERR] {e.Data}"
                        });
                    }
                };
                visionProcess.BeginOutputReadLine();
                visionProcess.BeginErrorReadLine();
            }

            process = _processSpawner.SpawnWorkerProcess(exePath, $"--pipe {pipeName} --id {workerId}", botFolder);
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    OnWorkerLog?.Invoke(workerId, new WorkerLogEntry
                    {
                        WorkerId = workerId,
                        Level = "INFO",
                        Message = $"[PROC-OUT] {e.Data}"
                    });
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    OnWorkerLog?.Invoke(workerId, new WorkerLogEntry
                    {
                        WorkerId = workerId,
                        Level = "ERROR",
                        Message = $"[PROC-ERR] {e.Data}"
                    });
                }
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            if (visionProcess != null)
            {
                try { _processSpawner.KillProcess(visionProcess); } catch { }
                try { visionProcess.Dispose(); } catch { }
            }
            pipeServer.Dispose();
            _logger?.LogError($"Error spawning worker: {ex.Message}");
            throw;
        }

        var worker = new WorkerProcess
        {
            WorkerId = workerId,
            Process = process,
            VisionProcess = visionProcess,
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
                OnWorkerLog?.Invoke(workerId, new WorkerLogEntry
                {
                    WorkerId = workerId,
                    Level = "ERROR",
                    Message = $"Worker process exited with code {exitCode}"
                });

                _tokenService.ReleaseAllForWorker(workerId);
                // FIX-1 (Audit): exitCode -1 IS now a crash (unhandled exception).
                // Only -2 (deliberate startup timeout) is treated as non-crash.
                // FIX-4 (Audit): exitCode -3 = hardware unavailable — crash but NO auto-restart.
                bool isStartTimeout = exitCode == -2;
                bool isCrash = exitCode != 0 && exitCode != -2;
                bool isHardwareMissing = exitCode == -3;
                bool isNativeCrash = exitCode != 0 && exitCode != -1 && exitCode != -2 && exitCode != -3 && exitCode != -4;
                bool isReaped = false;
                
                if (_intentionalStops.TryRemove(workerId, out var stopReason))
                {
                    isCrash = false;
                    isHardwareMissing = false;
                    isNativeCrash = false;
                    // Audit FIX-6: Do not clear history on intentional stop to avoid bypassing crash loop memory
                    
                    // Audit FIX-3: Don't overwrite ZombieReaped status with Stopped
                    if (stopReason == "reaped")
                    {
                        isReaped = true;
                    }
                }

                string status;
                if (isStartTimeout)
                {
                    status = WorkerStates.StartTimeout;
                }
                else if (isCrash)
                {
                    status = isHardwareMissing ? WorkerStates.HardwareMissing
                        : (isNativeCrash ? WorkerStates.Crashed : WorkerStates.Crashed);
                }
                else
                {
                    status = isReaped ? WorkerStates.ZombieReaped : WorkerStates.Stopped;
                }
                OnWorkerStatusChanged?.Invoke(workerId, status);

                bool shouldRestart = false;
                if (_workers.TryGetValue(workerId, out var w))
                {
                    shouldRestart = AutoRestart && (isCrash || isReaped) && !isStartTimeout && !isHardwareMissing && !isNativeCrash && !_disposed && w.IsInitialized && !_isShuttingDown;
                }

                await RemoveWorkerAsync(workerId);

                if (shouldRestart)
                {
                    bool isLooping = _crashProtector.RegisterCrashAndCheckIfLooping(workerId);
                    if (isLooping)
                    {
                        _logger?.LogError($"CRASH LOOP DETECTED for '{workerId}'. Stopping auto-restart.");
                        OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.CrashLoopStopped);
                        return;
                    }

                    OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Restarting);
                    int delay = isReaped ? Math.Max(RestartDelayMs, 8000) : RestartDelayMs;
                    // FIX C-3 (Audit): Observe _shutdownCts so StopAll can cancel pending restart delays
                    try { await Task.Delay(delay, _shutdownCts.Token); }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogInformation($"Restart delay for '{workerId}' cancelled by StopAll.");
                        OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Stopped);
                        return;
                    }

                    if (!_disposed && !_isShuttingDown && AutoRestart && !_intentionalStops.ContainsKey(workerId))
                    {
                        try
                        {
                            string botDir = File.Exists(startupArgs.BotDllPath) 
                                ? Path.GetDirectoryName(startupArgs.BotDllPath)! 
                                : startupArgs.BotDllPath;
                            if (OnAutoRestartRequested != null)
                            {
                                await OnAutoRestartRequested(startupArgs, botDir);
                            }
                            else
                            {
                                await StartWorkerAsync(startupArgs, botDir);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError($"Failed to restart Worker '{workerId}': {ex.Message}");
                            OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.RestartFailed);
                        }
                    }
                    else if (shouldRestart)
                    {
                        // Restart was intended but conditions changed during delay (e.g., disposed or shutting down)
                        OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Stopped);
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
            
            void OnEarlyExit(object? sender, EventArgs e) 
            {
                try { connectCts.Cancel(); } catch { }
            }
            process.Exited += OnEarlyExit;
            try
            {
                if (process.HasExited) connectCts.Cancel();
                await _pipeRegistry.WaitForConnectionAsync(pipeServer, AutomationDefaults.DefaultWorkerConnectTimeoutMs, connectCts.Token);
            }
            finally
            {
                process.Exited -= OnEarlyExit;
            }
        }
        catch (OperationCanceledException)
        {
            _intentionalStops.TryAdd(workerId, "timeout");
            _processSpawner.KillProcess(process);
            pipeServer.Dispose();
            if (process.HasExited) throw new Exception($"Worker '{workerId}' crashed before connection.");
            throw new TimeoutException($"Worker '{workerId}' connection timed out.");
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
                while (!readyCts.IsCancellationRequested)
                {
                    var readyLine = await worker.Reader.ReadLineAsync(readyCts.Token);
                    var readyMsg = IpcMessage.FromJson(readyLine ?? "");

                    if (readyMsg?.Type == IpcMessageTypes.Ready)
                    {
                        break;
                    }
                    else
                    {
                        _logger?.LogWarning($"Worker '{workerId}' didn't send Ready. Got: {readyMsg?.Type}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _intentionalStops.TryAdd(workerId, "timeout");
                OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.TimeoutReady);
                _processSpawner.KillProcess(process);
                await RemoveWorkerAsync(workerId);
                
                if (cancellationToken.IsCancellationRequested)
                    throw;
                else
                    throw new TimeoutException($"Worker '{workerId}' failed to send Ready within 10s");
            }
        }

        // 6. Send start command
        var startMsg = IpcMessage.Create(IpcMessageTypes.Start, startupArgs);
        await worker.Writer.WriteLineAsync(startMsg.ToJson());

        worker.IsInitialized = true;

        OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Running);

        // Start background message listener
        _ = Task.Run(() => _ipcListener.ListenAsync(worker), worker.Cts.Token);



        return workerId;
    }

    public async Task StopWorkerAsync(string workerId)
    {
        _workers.TryGetValue(workerId, out IWorkerProcess? worker);
        if (worker == null) return;

        try
        {
            var stack = new System.Diagnostics.StackTrace(1, false)
                .GetFrames()?
                .Select(frame => frame.GetMethod())
                .Where(m => m != null)
                .Select(m => $"{m!.DeclaringType?.FullName}.{m.Name}")
                .Take(6);
            var reason = stack == null ? "unknown" : string.Join(" <- ", stack);
            OnWorkerLog?.Invoke(workerId, new WorkerLogEntry
            {
                WorkerId = workerId,
                Level = "ERROR",
                Message = $"[MANAGER-STOP] StopWorkerAsync invoked by: {reason}"
            });
        }
        catch { }

        _intentionalStops.TryAdd(workerId, "stopped");
        OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Stopping);
        
        bool killedSuccessfully = true;

        try
        {
            var stopMsg = IpcMessage.Create(IpcMessageTypes.Stop);
            await worker.Writer.WriteLineAsync(stopMsg.ToJson());

            // AUDIT FIX (CORRECTION-5): Use async WaitForExitAsync instead of blocking WaitForExit
            bool exited;
            using (var exitCts = new CancellationTokenSource(ShutdownTimeoutMs))
            {
                try
                {
                    await worker.Process.WaitForExitAsync(exitCts.Token);
                    exited = true;
                }
                catch (OperationCanceledException)
                {
                    exited = false;
                }
            }
            if (!exited)
            {
                _logger?.LogWarning($"Worker '{workerId}' didn't stop gracefully. Killing.");
                try 
                { 
                    _processSpawner.KillProcess(worker.Process); 
                }
                catch (Exception killEx)
                {
                    _logger?.LogError($"Failed to kill Worker '{workerId}' after timeout: {killEx.Message}");
                    OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.HandlerError);
                    killedSuccessfully = false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error stopping Worker '{workerId}': {ex.Message}");
            try 
            { 
                _processSpawner.KillProcess(worker.Process); 
            }
            catch (Exception killEx)
            {
                _logger?.LogError($"Failed to forcefully kill Worker '{workerId}': {killEx.Message}");
                OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.HandlerError);
                killedSuccessfully = false;
            }
        }
        
        if (!killedSuccessfully)
        {
            await RemoveWorkerAsync(workerId);
        }
    }

    public async Task StopAllWorkersAsync()
    {
        _isShuttingDown = true;
        // FIX C-3 (Audit): Cancel any pending restart delays immediately
        _shutdownCts.Cancel();
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
            var oldCts = _shutdownCts;
            _shutdownCts = new CancellationTokenSource();
            oldCts.Dispose();
            _isShuttingDown = false;
        }
    }

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _writeLocks = new();

    /// <summary>
    /// Send an IPC message to a specific running worker.
    /// Returns true if the message was written to the pipe successfully.
    /// </summary>
    public async Task<bool> SendMessageToWorkerAsync(string workerId, IpcMessage message)
    {
        if (!_workers.TryGetValue(workerId, out var worker))
        {
            _logger?.LogWarning("SendMessageToWorkerAsync: Worker '{WorkerId}' not found.", workerId);
            return false;
        }

        if (worker.Writer == null)
        {
            _logger?.LogWarning("SendMessageToWorkerAsync: Worker '{WorkerId}' has no pipe writer.", workerId);
            return false;
        }

        var writeLock = _writeLocks.GetOrAdd(workerId, _ => new SemaphoreSlim(1, 1));
        if (!await writeLock.WaitAsync(TimeSpan.FromSeconds(3)))
        {
            _logger?.LogError("SendMessageToWorkerAsync: Timeout acquiring IPC write lock for '{WorkerId}'. Server IPC is suspended.", workerId);
            _ = RemoveWorkerAsync(workerId);
            return false;
        }

        try
        {
            // P2 (Audit): Enforce 3s write timeout natively using .NET 8 WaitAsync
            await worker.Writer.WriteLineAsync(message.ToJson()).WaitAsync(TimeSpan.FromSeconds(3));
            return true;
        }
        catch (TimeoutException)
        {
            _logger?.LogError("SendMessageToWorkerAsync: Pipe write operation timed out for '{WorkerId}'. Disconnecting.", workerId);
            _ = RemoveWorkerAsync(workerId);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError("SendMessageToWorkerAsync: Failed to send to '{WorkerId}': {Error}", workerId, ex.Message);
            return false;
        }
        finally
        {
            writeLock.Release();
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

    /// <summary>
    /// Sends a RequestVariables IPC message to a worker and waits for the VariablesSnapshot reply.
    /// Returns null if the worker is not found, pipe is broken, or the request times out.
    /// </summary>
    public async Task<Dictionary<string, System.Text.Json.JsonElement>?> RequestVariablesAsync(string workerId, int timeoutMs = 5000)
    {
        // 1. Register the wait BEFORE sending the request to avoid race conditions
        var waitTask = _ipcListener.WaitForVariablesAsync(workerId, timeoutMs);

        // 2. Send the request
        var msg = IpcMessage.Create(IpcMessageTypes.RequestVariables);
        var sent = await SendMessageToWorkerAsync(workerId, msg);
        if (!sent)
        {
            _logger?.LogWarning("RequestVariablesAsync: Failed to send request to worker '{WorkerId}'.", workerId);
            return null;
        }

        // 3. Wait for the response
        return await waitTask;
    }



    private Task RemoveWorkerAsync(string workerId)
    {
        // FIX P-1 (Audit): Do NOT clear _intentionalStops here.
        // The Process.Exited handler (line ~172) is the correct consumer of this flag.
        // Clearing it here races with the Exited handler and can cause
        // the exit to be misclassified as a crash, triggering unwanted auto-restart.
        if (_workers.TryRemove(workerId, out var worker))
        {
            worker.Cts.Cancel();
            try { worker.Writer?.Dispose(); } catch { }
            try { worker.Reader?.Dispose(); } catch { }
            try { worker.PipeServer?.Dispose(); } catch { }
            try
            {
                if (worker.VisionProcess is { HasExited: false })
                    _processSpawner.KillProcess(worker.VisionProcess);
            }
            catch { }
            try { worker.VisionProcess?.Dispose(); } catch { }
            try { worker.Process?.Dispose(); } catch { }
            
            _heartbeatMonitor.Clear(workerId);
            _logStreamer.CloseWriter(workerId);
            if (_writeLocks.TryRemove(workerId, out var writeLock))
            {
                writeLock.Dispose();
            }
        }
        return Task.CompletedTask;
    }

    private Process StartVisionServerProcess(string visionPipeName)
    {
        if (!File.Exists(VisionServerExePath))
            throw new FileNotFoundException($"VisionServer executable not found: {VisionServerExePath}");

        var workingDirectory = Path.GetDirectoryName(VisionServerExePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        _logger?.LogInformation("Starting VisionServer with pipe '{PipeName}'", visionPipeName);
        return _processSpawner.SpawnWorkerProcess(VisionServerExePath, $"--pipe {visionPipeName}", workingDirectory);
    }

    public void ClearCrashHistory(string workerId)
    {
        _crashProtector.ClearHistory(workerId);
    }

    /// <summary>
    /// AUDIT FIX (CORRECTION-1): Publicly marks a workerId as intentionally stopped.
    /// Called by WorkerOrchestrator.ClearAsync to prevent a pending auto-restart
    /// continuation from spawning an orphan worker after the slot is cleared.
    /// </summary>
    public void MarkIntentionalStop(string workerId)
    {
        _intentionalStops.TryAdd(workerId, "cleared");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _shutdownCts.Cancel(); } catch { }
        _shutdownCts.Dispose();

        _zombieReaper?.Dispose();
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


}
