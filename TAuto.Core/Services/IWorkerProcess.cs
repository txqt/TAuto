using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AutoBot.Shared.Ipc;

namespace TAuto.Core.Services;

/// <summary>
/// Interface contract for a running worker process.
/// </summary>
public interface IWorkerProcess
{
    string WorkerId { get; }
    Process Process { get; }
    Process? VisionProcess { get; set; }
    NamedPipeServerStream PipeServer { get; }
    StreamReader Reader { get; }
    StreamWriter Writer { get; }
    WorkerStartupArgs StartupArgs { get; }
    CancellationTokenSource Cts { get; }
    DateTime StartTimeUtc { get; }
    bool IsInitialized { get; set; }

    /// <summary>
    /// Channel for enqueuing messages to be written to the worker's pipe.
    /// </summary>
    Channel<string> MessageChannel { get; }

    /// <summary>
    /// The background task responsible for writing messages to the pipe.
    /// </summary>
    Task? WriterTask { get; set; }
}

public class WorkerProcess : IWorkerProcess
{
    public string WorkerId { get; set; } = string.Empty;
    public Process Process { get; set; } = null!;
    public Process? VisionProcess { get; set; }
    public NamedPipeServerStream PipeServer { get; set; } = null!;
    public StreamReader Reader { get; set; } = null!;
    public StreamWriter Writer { get; set; } = null!;
    public WorkerStartupArgs StartupArgs { get; set; } = null!;
    public CancellationTokenSource Cts { get; set; } = null!;
    public DateTime StartTimeUtc { get; set; }
    public bool IsInitialized { get; set; } = false;

    public Channel<string> MessageChannel { get; } = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public Task? WriterTask { get; set; }

    // Delegate tracking to break event handler roots and prevent memory leaks
    public EventHandler? ExitedHandler { get; set; }
    public DataReceivedEventHandler? OutputHandler { get; set; }
    public DataReceivedEventHandler? ErrorHandler { get; set; }
    public DataReceivedEventHandler? VisionOutputHandler { get; set; }
    public DataReceivedEventHandler? VisionErrorHandler { get; set; }
}

