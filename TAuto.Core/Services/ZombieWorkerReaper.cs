using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using AutoBot.Shared.Ipc;

namespace TAuto.Core.Services;

/// <summary>
/// Monitors active workers and terminates zombies that stop emitting consistent heartbeats.
/// </summary>
public class ZombieWorkerReaper : IDisposable
{
    private readonly ConcurrentDictionary<string, IWorkerProcess> _workers;
    private readonly ConcurrentDictionary<string, string> _intentionalStops;
    private readonly IHeartbeatMonitor _heartbeatMonitor;
    private readonly IProcessSpawner _processSpawner;
    private readonly ILogger? _logger;
    private readonly Action<string, string>? _onWorkerStatusChanged;
    
    private Timer? _heartbeatReaperTimer;
    private bool _disposed;
    private readonly ConcurrentDictionary<string, int> _missedHeartbeats = new();

    public int HeartbeatTimeoutSeconds { get; set; } = 15;

    public ZombieWorkerReaper(
        ConcurrentDictionary<string, IWorkerProcess> workers,
        ConcurrentDictionary<string, string> intentionalStops,
        IHeartbeatMonitor heartbeatMonitor,
        IProcessSpawner processSpawner,
        ILogger? logger,
        Action<string, string>? onWorkerStatusChanged)
    {
        _workers = workers;
        _intentionalStops = intentionalStops;
        _heartbeatMonitor = heartbeatMonitor;
        _processSpawner = processSpawner;
        _logger = logger;
        _onWorkerStatusChanged = onWorkerStatusChanged;

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
                    var age = (DateTime.UtcNow - worker.StartTimeUtc).TotalSeconds;
                    if (age > 30)
                    {
                        _logger?.LogWarning("HEARTBEAT REAPER: Worker '{WorkerId}' failed to send initial heartbeat within 30s. Killing.", worker.WorkerId);
                        _intentionalStops.TryAdd(worker.WorkerId, "reaped");
                        _processSpawner.KillProcess(worker.Process);
                        _onWorkerStatusChanged?.Invoke(worker.WorkerId, WorkerStates.ZombieReaped);
                    }
                    continue;
                }

                var elapsed = (DateTime.UtcNow - lastHb.Value).TotalSeconds;
                if (elapsed > HeartbeatTimeoutSeconds)
                {
                    int missedCount = _missedHeartbeats.AddOrUpdate(worker.WorkerId, 1, (_, count) => count + 1);
                    if (missedCount >= 3)
                    {
                        _logger?.LogWarning(
                            "HEARTBEAT REAPER: Worker '{WorkerId}' missed 3 consecutive heartbeats (last seen {Elapsed:F0}s ago). Killing.",
                            worker.WorkerId, elapsed);
                        _intentionalStops.TryAdd(worker.WorkerId, "reaped");
                        _processSpawner.KillProcess(worker.Process);
                        _onWorkerStatusChanged?.Invoke(worker.WorkerId, WorkerStates.ZombieReaped);
                    }
                }
                else
                {
                    _missedHeartbeats.TryRemove(worker.WorkerId, out _);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Heartbeat reaper error for worker '{WorkerId}': {Error}", kvp.Key, ex.Message);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _heartbeatReaperTimer?.Dispose();
    }
}
