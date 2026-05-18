using System;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;

namespace TAuto.Core.Services;

public class DefaultNamedPipeRegistry : INamedPipeRegistry
{
    public NamedPipeServerStream CreatePipeServer(string pipeName)
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
        {
            var pipeSecurity = new PipeSecurity();
            var currentUserSid = System.Security.Principal.WindowsIdentity.GetCurrent().User;
            if (currentUserSid != null)
            {
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    currentUserSid,
                    PipeAccessRights.FullControl,
                    System.Security.AccessControl.AccessControlType.Allow));
            }

            var adminSid = new System.Security.Principal.SecurityIdentifier(
                System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null);
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                adminSid,
                PipeAccessRights.FullControl,
                System.Security.AccessControl.AccessControlType.Allow));

            return NamedPipeServerStreamAcl.Create(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                inBufferSize: 0,
                outBufferSize: 0,
                pipeSecurity);
        }

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
