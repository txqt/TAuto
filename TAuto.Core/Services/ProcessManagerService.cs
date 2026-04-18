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
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _workerLocks = new();
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
            
        var workerLock = _workerLocks.GetOrAdd(workerId, _ => new SemaphoreSlim(1, 1));
        await workerLock.WaitAsync(cancellationToken);
        try
        {
            
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
        bool isNativeBot = false;
        try
        {
            var exePath = WorkerExePath;
            isNativeBot = !string.IsNullOrEmpty(startupArgs.NativeExePath) &&
                              startupArgs.NativeExePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                              File.Exists(startupArgs.NativeExePath);
            
            // Support starting a Native AOT bot directly
            if (isNativeBot)
            {
                exePath = startupArgs.NativeExePath;
                _logger?.LogInformation($"Native AOT Executable detected: Overriding worker EXE to '{exePath}'");

                startupArgs.VisionPipeName = $"AutoBot_Vision_{workerId}_{Guid.NewGuid():N}";
                visionProcess = StartVisionServerProcess(startupArgs.VisionPipeName);
            }

            process = _processSpawner.SpawnWorkerProcess(exePath, $"--pipe {pipeName} --id {workerId}", botFolder);
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

        // 3. Attach Event Handlers (Stored in WorkerProcess to allow clean unsubscription)
        if (isNativeBot && visionProcess != null)
        {
            worker.VisionOutputHandler = (_, e) =>
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
            worker.VisionErrorHandler = (_, e) =>
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
            visionProcess.OutputDataReceived += worker.VisionOutputHandler;
            visionProcess.ErrorDataReceived += worker.VisionErrorHandler;
            visionProcess.BeginOutputReadLine();
            visionProcess.BeginErrorReadLine();
        }

        worker.OutputHandler = (_, e) =>
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
        worker.ErrorHandler = (_, e) =>
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
        process.OutputDataReceived += worker.OutputHandler;
        process.ErrorDataReceived += worker.ErrorHandler;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Break closure capture of large startupArgs object
        string capturedBotDllPath = startupArgs.BotDllPath;
        var capturedStartupArgs = startupArgs;

        // FIX-3: Attach Process Monitors Immediately
        worker.ExitedHandler = async (_, _) =>
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
                // Use the closure's 'worker' instance instead of querying the dictionary to avoid races if worker was replaced
                shouldRestart = AutoRestart && (isCrash || isReaped) && !isStartTimeout && !isHardwareMissing && !isNativeCrash && !_disposed && worker.IsInitialized && !_isShuttingDown;

                await RemoveWorkerAsync(workerId, worker);

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
                            string botDir = File.Exists(capturedBotDllPath) 
                                ? Path.GetDirectoryName(capturedBotDllPath)! 
                                : capturedBotDllPath;
                            if (OnAutoRestartRequested != null)
                            {
                                await OnAutoRestartRequested(capturedStartupArgs, botDir);
                            }
                            else
                            {
                                await StartWorkerAsync(capturedStartupArgs, botDir);
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
        process.Exited += worker.ExitedHandler;


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
        var writeStartTask = worker.Writer.WriteLineAsync(startMsg.ToJson());
        try
        {
            await writeStartTask.WaitAsync(TimeSpan.FromMilliseconds(2000));
        }
        catch (TimeoutException)
        {
            _logger?.LogWarning($"StartWorkerAsync: WriteLineAsync timed out for '{workerId}'. Continuing anyway.");
        }

        worker.IsInitialized = true;

        OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Running);

        // Start background message listener
        _ = Task.Run(() => _ipcListener.ListenAsync(worker), worker.Cts.Token);

        // LATE STOP DETECTION: If a stop was intentionally requested while this was spinning up
        if (_intentionalStops.ContainsKey(workerId))
        {
            _logger?.LogWarning($"Late stop detected for '{workerId}' immediately after start. Killing process.");
            try { _processSpawner.KillProcess(worker.Process); } catch { }
            if (worker.VisionProcess != null) try { _processSpawner.KillProcess(worker.VisionProcess); } catch { }
            OnWorkerStatusChanged?.Invoke(workerId, WorkerStates.Stopped);
            _workers.TryRemove(workerId, out _);
            return workerId;
        }

        return workerId;
        }
        finally
        {
            try { workerLock?.Release(); } catch (ObjectDisposedException) { }
        }
    }

    public async Task StopWorkerAsync(string workerId)
    {
        var workerLock = _workerLocks.GetOrAdd(workerId, _ => new SemaphoreSlim(1, 1));
        bool lockAcquired = false;
        try
        {
            lockAcquired = await workerLock.WaitAsync(TimeSpan.FromMilliseconds(ShutdownTimeoutMs));
        }
        catch (ObjectDisposedException) { }

        if (!lockAcquired)
        {
            _logger?.LogWarning($"StopWorkerAsync: Could not acquire lock for '{workerId}', proceeding to force stop.");
        }

        try
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
                var writeTask = worker.Writer.WriteLineAsync(stopMsg.ToJson());
                try
                {
                    await writeTask.WaitAsync(TimeSpan.FromMilliseconds(2000));
                }
                catch (TimeoutException)
                {
                    _logger?.LogWarning($"StopWorkerAsync: WriteLineAsync timed out for '{workerId}'.");
                }

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
                        if (worker.VisionProcess != null) { _processSpawner.KillProcess(worker.VisionProcess); }
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
                    if (worker.VisionProcess != null) { _processSpawner.KillProcess(worker.VisionProcess); }
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
        finally
        {
            if (lockAcquired)
            {
                try { workerLock?.Release(); } catch (ObjectDisposedException) { }
            }
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
            try { writeLock?.Release(); } catch (ObjectDisposedException) { }
        }
    }

    public List<(string Id, bool IsRunning, long MemoryBytes)> GetWorkerStatuses()
    {
        var results = new List<(string Id, bool IsRunning, long MemoryBytes)>();
        foreach (var w in _workers.Values)
        {
            try
            {
                // P0-4: Guard against ObjectDisposedException
                var proc = w.Process;
                bool isRunning = proc != null && !proc.HasExited;
                long mem = _heartbeatMonitor.GetLastHeartbeat(w.WorkerId)?.MemoryBytes ?? 0;
                results.Add((w.WorkerId, isRunning, mem));
            }
            catch { /* Ignore disposed processes */ }
        }
        return results;
    }

    /// <summary>
    /// Safely checks if a worker process is still alive without risking ObjectDisposedException or dictionary race.
    /// </summary>
    public bool IsWorkerAlive(string workerId)
    {
        if (!_workers.TryGetValue(workerId, out var worker)) return false;
        try
        {
            var proc = worker.Process;
            return proc != null && !proc.HasExited;
        }
        catch { return false; }
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



    private Task RemoveWorkerAsync(string workerId, IWorkerProcess? expectedWorker = null)
    {
        bool isCurrentWorker = true;
        
        if (expectedWorker != null)
        {
            // Conditional atomic removal
            var dict = (ICollection<KeyValuePair<string, IWorkerProcess>>)_workers;
            isCurrentWorker = dict.Remove(new KeyValuePair<string, IWorkerProcess>(workerId, expectedWorker));
        }
        else
        {
            isCurrentWorker = _workers.TryRemove(workerId, out expectedWorker);
        }

        var workerToClean = expectedWorker;
        if (workerToClean != null)
        {
            // 1. Unsubscribe from all event handlers to break GC roots
            if (workerToClean is WorkerProcess wp)
            {
                if (wp.Process != null)
                {
                    if (wp.ExitedHandler != null) try { wp.Process.Exited -= wp.ExitedHandler; } catch { }
                    if (wp.OutputHandler != null) try { wp.Process.OutputDataReceived -= wp.OutputHandler; } catch { }
                    if (wp.ErrorHandler != null) try { wp.Process.ErrorDataReceived -= wp.ErrorHandler; } catch { }
                }

                if (wp.VisionProcess != null)
                {
                    if (wp.VisionOutputHandler != null) try { wp.VisionProcess.OutputDataReceived -= wp.VisionOutputHandler; } catch { }
                    if (wp.VisionErrorHandler != null) try { wp.VisionProcess.ErrorDataReceived -= wp.VisionErrorHandler; } catch { }
                }

                // Clear delegate references
                wp.ExitedHandler = null;
                wp.OutputHandler = null;
                wp.ErrorHandler = null;
                wp.VisionOutputHandler = null;
                wp.VisionErrorHandler = null;
            }

            workerToClean.Cts?.Cancel();
            try { workerToClean.Cts?.Dispose(); } catch { }
            try { workerToClean.Writer?.Dispose(); } catch { }
            try { workerToClean.Reader?.Dispose(); } catch { }
            try { workerToClean.PipeServer?.Dispose(); } catch { }
            try
            {
                if (workerToClean.VisionProcess is { HasExited: false })
                    _processSpawner.KillProcess(workerToClean.VisionProcess);
            }
            catch { }
            try { workerToClean.VisionProcess?.Dispose(); } catch { }
            try { workerToClean.Process?.Dispose(); } catch { }

            // Break reference to large startup object to assist GC
            if (workerToClean is WorkerProcess wp2)
            {
                wp2.StartupArgs = null!;
            }
        }

        // Only clean up global state (locks, heartbeats) if this was the current active worker for this ID
        if (isCurrentWorker)
        {
            _heartbeatMonitor.Clear(workerId);
            _logStreamer.CloseWriter(workerId);
            
            if (_workerLocks.TryRemove(workerId, out var workerLock)) workerLock.Dispose();
            if (_writeLocks.TryRemove(workerId, out var writeLock)) writeLock.Dispose();
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
        _memoryDiagnosticsTimer?.Dispose(); // FIX: Dispose timer
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
