using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TAuto.Core.Diagnostics;

public static class MemoryDiagnostics
{
    public static string GetMemorySnapshot(string context = "Worker")
    {
        var process = Process.GetCurrentProcess();
        return $"[{context}] Memory: WorkingSet={process.WorkingSet64 / 1048576.0:F1}MB, Private={process.PrivateMemorySize64 / 1048576.0:F1}MB, GC.Total={GC.GetTotalMemory(false) / 1048576.0:F1}MB, Gen0={GC.CollectionCount(0)}, Gen1={GC.CollectionCount(1)}, Gen2={GC.CollectionCount(2)}";
    }

    public static void LogMemorySnapshot(ILogger logger, string context = "Worker")
    {
        logger.LogInformation("{Snapshot}", GetMemorySnapshot(context));
    }
}
