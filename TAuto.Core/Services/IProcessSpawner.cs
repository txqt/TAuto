using System.Diagnostics;

namespace TAuto.Core.Services;

public interface IProcessSpawner
{
    Process SpawnWorkerProcess(string exePath, string arguments, string workingDirectory);
    void KillProcess(Process process);
    void TerminateAll();
}
