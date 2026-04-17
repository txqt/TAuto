using System;
using System.Diagnostics;
using System.IO;

namespace TAuto.Core.Services;

public class DefaultProcessSpawner : IProcessSpawner, IDisposable
{
    private readonly JobObject _jobObject;
    private bool _disposed;

    public DefaultProcessSpawner()
    {
        _jobObject = new JobObject();
    }

    public Process SpawnWorkerProcess(string exePath, string arguments, string workingDirectory)
    {
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException($"Worker executable not found: {exePath}");
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                // Verb = "runas",
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        process.Start();
        
        try { _jobObject.AssignProcess(process); } catch { }

        return process;
    }

    public void KillProcess(Process process)
    {
        try { process.Kill(true); } catch { }
    }

    public void TerminateAll()
    {
        _jobObject.TerminateAll();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _jobObject.TerminateAll();
        _jobObject.Dispose();
    }
}
