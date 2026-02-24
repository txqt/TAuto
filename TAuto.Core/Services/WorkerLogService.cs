using System;
using System.Collections.Concurrent;
using System.IO;

namespace TAuto.Core.Services;

/// <summary>
/// Writes structured per-worker log files for retrospective debugging.
/// Each Worker gets its own log file: worker-{id}_{date}.log
/// 
/// Thread-safe: uses ConcurrentDictionary for writer lookup + lock per writer.
/// Designed to be wired to ProcessManagerService.OnWorkerLog.
/// </summary>
public class WorkerLogService : ILogStreamer
{
    private readonly string _logDirectory;
    private readonly ConcurrentDictionary<string, StreamWriter> _writers = new();
    private bool _disposed;

    public WorkerLogService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _logDirectory = Path.Combine(appData, "AutoBot", "Logs", "Workers");
        Directory.CreateDirectory(_logDirectory);
    }

    /// <summary>
    /// Write a structured log entry for a specific worker.
    /// Call this from ProcessManagerService.OnWorkerLog event.
    /// </summary>
    public void WriteLog(string workerId, string level, string message)
    {
        if (_disposed) return;

        try
        {
            var writer = GetOrCreateWriter(workerId);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var line = $"[{timestamp}] [{level,-5}] {message}";

            lock (writer)
            {
                writer.WriteLine(line);
                writer.Flush();
            }
        }
        catch
        {
            // Best-effort logging — never crash the Manager
        }
    }

    /// <summary>
    /// Log a lifecycle event (started, stopped, crashed, restarting, crash_loop_stopped).
    /// </summary>
    public void WriteStatus(string workerId, string status)
    {
        WriteLog(workerId, "EVENT", $"Worker status changed: {status}");
    }

    private StreamWriter GetOrCreateWriter(string workerId)
    {
        return _writers.GetOrAdd(workerId, id =>
        {
            var date = DateTime.Now.ToString("yyyy-MM-dd");
            var filename = $"worker-{id}_{date}.log";
            var path = Path.Combine(_logDirectory, filename);
            return new StreamWriter(path, append: true) { AutoFlush = false };
        });
    }

    /// <summary>
    /// Close and remove the writer for a specific worker (e.g., on worker removal).
    /// Next log for this worker will create a fresh file.
    /// </summary>
    public void CloseWriter(string workerId)
    {
        if (_writers.TryRemove(workerId, out var writer))
        {
            try { writer.Flush(); writer.Dispose(); } catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var kvp in _writers)
        {
            try { kvp.Value.Flush(); kvp.Value.Dispose(); } catch { }
        }
        _writers.Clear();
    }
}
