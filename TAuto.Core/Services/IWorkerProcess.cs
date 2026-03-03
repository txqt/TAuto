using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using AutoBot.Shared.Ipc;

namespace TAuto.Core.Services;

/// <summary>
/// Interface contract for a running worker process.
/// </summary>
public interface IWorkerProcess
{
    string WorkerId { get; }
    Process Process { get; }
    NamedPipeServerStream PipeServer { get; }
    StreamReader Reader { get; }
    StreamWriter Writer { get; }
    WorkerStartupArgs StartupArgs { get; }
    CancellationTokenSource Cts { get; }
    DateTime StartTimeUtc { get; }
    bool IsInitialized { get; set; }
}

public class WorkerProcess : IWorkerProcess
{
    public string WorkerId { get; set; } = string.Empty;
    public Process Process { get; set; } = null!;
    public NamedPipeServerStream PipeServer { get; set; } = null!;
    public StreamReader Reader { get; set; } = null!;
    public StreamWriter Writer { get; set; } = null!;
    public WorkerStartupArgs StartupArgs { get; set; } = null!;
    public CancellationTokenSource Cts { get; set; } = null!;
    public DateTime StartTimeUtc { get; set; }
    public bool IsInitialized { get; set; } = false;
}
