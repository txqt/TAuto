using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace TAuto.Core.Services;

public interface INamedPipeRegistry
{
    NamedPipeServerStream CreatePipeServer(string pipeName);
    Task WaitForConnectionAsync(NamedPipeServerStream pipeServer, int timeoutMs, CancellationToken ct);
}
