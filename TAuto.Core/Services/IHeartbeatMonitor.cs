using System;
using AutoBot.Shared.Ipc;

namespace TAuto.Core.Services;

public interface IHeartbeatMonitor
{
    void RecordHeartbeat(string workerId, WorkerHeartbeat heartbeat);
    WorkerHeartbeat? GetLastHeartbeat(string workerId);
    DateTime? GetLastHeartbeatTime(string workerId);
    void Clear(string workerId);
}
