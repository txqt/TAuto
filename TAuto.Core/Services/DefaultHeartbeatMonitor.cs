using System;
using System.Collections.Concurrent;
using TAuto.Shared.Ipc;

namespace TAuto.Core.Services;

public class DefaultHeartbeatMonitor : IHeartbeatMonitor
{
    private readonly ConcurrentDictionary<string, WorkerHeartbeat> _heartbeats = new();
    private readonly ConcurrentDictionary<string, DateTime> _heartbeatTimes = new();

    public void RecordHeartbeat(string workerId, WorkerHeartbeat heartbeat)
    {
        _heartbeats[workerId] = heartbeat;
        _heartbeatTimes[workerId] = DateTime.UtcNow;
    }

    public WorkerHeartbeat? GetLastHeartbeat(string workerId)
    {
        return _heartbeats.TryGetValue(workerId, out var hb) ? hb : null;
    }

    public DateTime? GetLastHeartbeatTime(string workerId)
    {
        return _heartbeatTimes.TryGetValue(workerId, out var time) ? time : null;
    }

    public void Clear(string workerId)
    {
        _heartbeats.TryRemove(workerId, out _);
        _heartbeatTimes.TryRemove(workerId, out _);
    }
}
