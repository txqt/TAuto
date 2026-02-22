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
using AutoBot.Shared.Ipc;

namespace TAuto.Core.Services;

/// <summary>
/// Manages Worker processes — spawn, monitor, restart, IPC.
/// 
/// Lifecycle per Worker:
/// 1. CreateWorkerAsync() → spawns AutoBot.Worker.exe, assigns to Job Object.
/// 2. Worker connects via Named Pipe → Manager sends "start" command.
/// 3. Worker sends heartbeats → Manager tracks health.
/// 4. Worker exits → Manager restarts if crash (auto-recovery).
/// 5. StopWorkerAsync() → graceful shutdown with timeout → hard kill.
/// </summary>
public class ProcessManagerService : IDisposable
{
    private static readonly Random _random = new();
    private readonly JobObject _jobObject;
    private readonly ConcurrentDictionary<string, WorkerProcess> _workers = new();
    private readonly ComputeTokenService _tokenService;
    private readonly WorkerLogService _workerLogService;
    private bool _disposed;

    // ── Crash Loop Protection ──
    private readonly ConcurrentDictionary<string, List<DateTime>> _crashHistory = new();
    private const int MaxCrashesBeforeStop = 5;
    private static readonly TimeSpan CrashWindowDuration = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Path to the Worker executable.
    /// </summary>
    public string WorkerExePath { get; set; }

    /// <summary>
    /// Time to wait before restarting a crashed Worker.
    /// </summary>
    public int RestartDelayMs { get; set; } = 5000;

    /// <summary>
    /// Timeout for graceful shutdown before hard-kill.
    /// </summary>
    public int ShutdownTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Enable auto-restart of crashed Workers.
    /// </summary>
    public bool AutoRestart { get; set; } = true;

    /// <summary>
    /// Fired when a Worker sends a log entry.
    /// </summary>
    public event Action<string, WorkerLogEntry>? OnWorkerLog;

    /// <summary>
    /// Fired when a Worker's status changes (started, stopped, crashed, restarting).
    /// </summary>
    public event Action<string, string>? OnWorkerStatusChanged;

    /// <summary>
    /// Fired when a Worker sends a heartbeat.
    /// </summary>
    public event Action<string, WorkerHeartbeat>? OnWorkerHeartbeat;

    public ProcessManagerService(ComputeTokenService? tokenService = null)
    {
        // Use an anonymous Job Object to prevent Win32Exception (5) Access Denied
        // if a previous instance left zombie processes holding the named handle.
        _jobObject = new JobObject();
        _tokenService = tokenService ?? new ComputeTokenService();
        _workerLogService = new WorkerLogService();

        // Wire structured per-worker logging
        OnWorkerLog += (workerId, log) =>
            _workerLogService.WriteLog(workerId, log.Level ?? "INFO", log.Message ?? "");
        OnWorkerStatusChanged += (workerId, status) =>
            _workerLogService.WriteStatus(workerId, status);

        // Default: same directory as Manager executable
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        WorkerExePath = Path.Combine(baseDir, "AutoBot.Worker.exe");
    }

    /// <summary>
    /// Spawn a new Worker process and connect via Named Pipe.
    /// </summary>
    public async Task<string> StartWorkerAsync(WorkerStartupArgs startupArgs, string botFolder)
    {
        var workerId = string.IsNullOrEmpty(startupArgs.WorkerId) 
            ? $"worker-{_random.Next(1000, 9999)}" 
            : startupArgs.WorkerId;
            
        startupArgs.WorkerId = workerId;
        var pipeName = $"AutoBot_Worker_{workerId}";

        OnWorkerStatusChanged?.Invoke(workerId, "starting");

        // ── Step 1: Create Named Pipe Server ──
        NamedPipeServerStream pipeServer;
        try
        {
            pipeServer = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }
        catch (Exception ex)
        {
            OnWorkerStatusChanged?.Invoke(workerId, $"pipe error: {ex.Message}");
            throw;
        }

        // ── Step 2: Spawn Worker Process ──
        if (!File.Exists(WorkerExePath))
        {
            pipeServer.Dispose();
            throw new FileNotFoundException($"Worker executable not found: {WorkerExePath}");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = WorkerExePath,
                Arguments = $"--pipe {pipeName} --id {workerId}",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = botFolder
            },
            EnableRaisingEvents = true
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            pipeServer.Dispose();
            throw;
        }

        try { _jobObject.AssignProcess(process); } catch { }

        // ── Step 3: Wait for Worker to connect (Task.WhenAny — CancellationToken doesn't cancel pipe I/O on Windows) ──
        {
            var connectTask = pipeServer.WaitForConnectionAsync(CancellationToken.None);
            var timeoutTask = Task.Delay(10000);
            if (await Task.WhenAny(connectTask, timeoutTask) != connectTask)
            {
                try { process.Kill(); } catch { }
                pipeServer.Dispose();
                throw new TimeoutException($"Worker '{workerId}' failed to connect within 10s");
            }
            await connectTask;
        }

        // ── Create WorkerProcess tracking object ──
        // IMPORTANT: Use UTF8 *without BOM* and set AutoFlush AFTER construction.
        // Setting AutoFlush=true in an initializer calls Flush() immediately,
        // which writes the UTF-8 BOM and calls FlushFileBuffers on the pipe — deadlock.
        var noBomUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var reader = new StreamReader(pipeServer, noBomUtf8, detectEncodingFromByteOrderMarks: false);
        var writer = new StreamWriter(pipeServer, noBomUtf8);
        writer.AutoFlush = true;

        var worker = new WorkerProcess
        {
            WorkerId = workerId,
            Process = process,
            PipeServer = pipeServer,
            Reader = reader,
            Writer = writer,
            StartupArgs = startupArgs,
            Cts = new CancellationTokenSource()
        };

        _workers[workerId] = worker;

        // ── Step 4: Wait for Ready signal ──
        {
            var readyTask = worker.Reader.ReadLineAsync();
            var timeoutTask = Task.Delay(10000);
            if (await Task.WhenAny(readyTask, timeoutTask) != readyTask)
            {
                OnWorkerStatusChanged?.Invoke(workerId, "TIMEOUT: ready signal");
                try { process.Kill(); } catch { }
                pipeServer.Dispose();
                await RemoveWorkerAsync(workerId);
                throw new TimeoutException($"Worker '{workerId}' failed to send Ready within 10s");
            }

            var readyLine = await readyTask;
            var readyMsg = IpcMessage.FromJson(readyLine ?? "");

            if (readyMsg?.Type != IpcMessageTypes.Ready)
            {
                Debug.WriteLine($"[Manager] Worker '{workerId}' didn't send Ready. Got: {readyMsg?.Type}");
            }
        }

        // ── Step 5: Send start command ──
        var startMsg = IpcMessage.Create(IpcMessageTypes.Start, startupArgs);
        await worker.Writer.WriteLineAsync(startMsg.ToJson());

        OnWorkerStatusChanged?.Invoke(workerId, "running");

        // ── Start background message listener ──
        _ = Task.Run(() => ListenToWorkerAsync(worker), worker.Cts.Token);

        // ── Monitor process exit (crash recovery with Crash Loop Protection) ──
        process.Exited += async (_, _) =>
        {
            // CRITICAL: This is async void (required by event signature).
            // Any unhandled exception here would crash the entire App.
            // Wrap everything in try-catch for safety.
            try
            {
                var exitCode = process.ExitCode;
                Debug.WriteLine($"[Manager] Worker '{workerId}' exited with code {exitCode}");

                // Release any tokens held by this worker to prevent leaks
                _tokenService.ReleaseAllForWorker(workerId);

                bool isCrash = exitCode != 0 && exitCode != -1;
                OnWorkerStatusChanged?.Invoke(workerId, isCrash ? "crashed" : "stopped");

                // Clean up
                await RemoveWorkerAsync(workerId);

                // Auto-restart only on crashes (not graceful stops)
                if (AutoRestart && isCrash && !_disposed)
                {
                    // ── Crash Loop Protection ──
                    var history = _crashHistory.GetOrAdd(workerId, _ => new List<DateTime>());
                    lock (history)
                    {
                        var now = DateTime.UtcNow;
                        // Prune old entries outside the window
                        history.RemoveAll(t => (now - t) > CrashWindowDuration);
                        history.Add(now);

                        if (history.Count > MaxCrashesBeforeStop)
                        {
                            Debug.WriteLine($"[Manager] CRASH LOOP DETECTED for '{workerId}': " +
                                $"{history.Count} crashes in {CrashWindowDuration.TotalSeconds}s. Stopping auto-restart.");
                            OnWorkerStatusChanged?.Invoke(workerId, "crash_loop_stopped");
                            return;
                        }
                    }

                    OnWorkerStatusChanged?.Invoke(workerId, "restarting");
                    await Task.Delay(RestartDelayMs);

                    if (!_disposed)
                    {
                        try
                        {
                            string botDir = Path.GetDirectoryName(startupArgs.BotDllPath)!;
                            await StartWorkerAsync(startupArgs, botDir);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Manager] Failed to restart Worker '{workerId}': {ex.Message}");
                            OnWorkerStatusChanged?.Invoke(workerId, "restart_failed");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Manager] CRITICAL: Worker '{workerId}' Exited handler failed: {ex.Message}");
                OnWorkerStatusChanged?.Invoke(workerId, $"handler_error: {ex.Message}");
            }
        };

        return workerId;
    }

    /// <summary>
    /// Gracefully stop a Worker (with timeout → hard kill).
    /// </summary>
    public async Task StopWorkerAsync(string workerId)
    {
        _workers.TryGetValue(workerId, out WorkerProcess? worker);

        if (worker == null) return;

        OnWorkerStatusChanged?.Invoke(workerId, "stopping");

        try
        {
            // 1. Send stop command
            var stopMsg = IpcMessage.Create(IpcMessageTypes.Stop);
            await worker.Writer.WriteLineAsync(stopMsg.ToJson());

            // 2. Wait for process to exit gracefully
            var exited = worker.Process.WaitForExit(ShutdownTimeoutMs);

            if (!exited)
            {
                // 3. Hard-kill after timeout
                Debug.WriteLine($"[Manager] Worker '{workerId}' didn't stop gracefully. Killing.");
                worker.Process.Kill();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Manager] Error stopping Worker '{workerId}': {ex.Message}");
            try { worker.Process.Kill(); } catch { }
        }
        finally
        {
            await RemoveWorkerAsync(workerId);
        }
    }

    /// <summary>
    /// Stop all Workers gracefully.
    /// </summary>
    public async Task StopAllWorkersAsync()
    {
        var workerIds = _workers.Keys.ToList();

        // Disable auto-restart during shutdown
        AutoRestart = false;

        // Parallel graceful stop
        var stopTasks = workerIds.Select(id => StopWorkerAsync(id));
        await Task.WhenAll(stopTasks);
    }

    /// <summary>
    /// Get current status of all Workers.
    /// </summary>
    public List<(string Id, bool IsRunning, long MemoryBytes)> GetWorkerStatuses()
    {
        return _workers.Values.Select(w => (
            w.WorkerId,
            !w.Process.HasExited,
            w.LastHeartbeat?.MemoryBytes ?? 0
        )).ToList();
    }

    // ════════════════════════════════════════════════════════════
    // Internal
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Background loop: listen for messages from a Worker.
    /// </summary>
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
                            worker.LastHeartbeat = hb;
                            worker.LastHeartbeatTime = DateTime.UtcNow;
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
                        Debug.WriteLine($"[Manager] Worker '{worker.WorkerId}' sent exit notification");
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
            Debug.WriteLine($"[Manager] Listener error for '{worker.WorkerId}': {ex.Message}");
        }
        finally
        {
            // Ensure tokens are released if listener exits unexpectedly
            _tokenService.ReleaseAllForWorker(worker.WorkerId);
        }
    }

    private Task RemoveWorkerAsync(string workerId)
    {
        if (_workers.TryRemove(workerId, out var worker))
        {
            worker.Cts.Cancel();
            // Pipe may already be broken/closed from Worker side — safe to ignore
            try { worker.Writer?.Dispose(); } catch { }
            try { worker.Reader?.Dispose(); } catch { }
            try { worker.PipeServer?.Dispose(); } catch { }
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Kill all Workers via Job Object (OS guarantee)
        _jobObject.TerminateAll();
        _jobObject.Dispose();
        _tokenService.Dispose();
        _workerLogService.Dispose();

        foreach (var worker in _workers.Values)
        {
            worker.Cts?.Cancel();
            worker.Writer?.Dispose();
            worker.Reader?.Dispose();
            worker.PipeServer?.Dispose();
        }
        _workers.Clear();
    }

    // ════════════════════════════════════════════════════════════
    // Internal Types
    // ════════════════════════════════════════════════════════════

    private class WorkerProcess
    {
        public string WorkerId { get; set; } = string.Empty;
        public Process Process { get; set; } = null!;
        public NamedPipeServerStream PipeServer { get; set; } = null!;
        public StreamReader Reader { get; set; } = null!;
        public StreamWriter Writer { get; set; } = null!;
        public WorkerStartupArgs StartupArgs { get; set; } = null!;
        public CancellationTokenSource Cts { get; set; } = null!;
        public WorkerHeartbeat? LastHeartbeat { get; set; }
        public DateTime? LastHeartbeatTime { get; set; }
    }
}
