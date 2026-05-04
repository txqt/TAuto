using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace TAuto.Core.Services;

public class DefaultProcessSpawner : IProcessSpawner, IDisposable
{
    private readonly JobObject _jobObject;
    private readonly ILogger<DefaultProcessSpawner>? _logger;
    private bool _disposed;

    public DefaultProcessSpawner(ILogger<DefaultProcessSpawner>? logger = null)
    {
        _jobObject = new JobObject();
        _logger = logger;
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
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        process.Start();
        
        try { _jobObject.AssignProcess(process); } 
        catch (Exception ex) { _logger?.LogWarning(ex, "Failed to assign process {Pid} to JobObject", process.Id); }

        return process;
    }

    public void KillProcess(Process process)
    {
        try { process.Kill(true); } 
        catch (Exception ex) { _logger?.LogTrace(ex, "Failed to kill process {Pid} (it may have already exited)", process.Id); }
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
