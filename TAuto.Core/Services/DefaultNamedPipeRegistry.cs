using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace TAuto.Core.Services;

public class DefaultNamedPipeRegistry : INamedPipeRegistry
{
    public NamedPipeServerStream CreatePipeServer(string pipeName)
    {
        return new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    public async Task WaitForConnectionAsync(NamedPipeServerStream pipeServer, int timeoutMs, CancellationToken ct)
    {
        var connectTask = pipeServer.WaitForConnectionAsync(ct);
        var timeoutTask = Task.Delay(timeoutMs, ct);
        if (await Task.WhenAny(connectTask, timeoutTask) != connectTask)
        {
            throw new TimeoutException($"Pipe failed to connect within {timeoutMs}ms");
        }
        await connectTask; // Propagate any exceptions from connectTask
    }
}
