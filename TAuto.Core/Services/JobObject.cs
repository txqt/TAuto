using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace TAuto.Core.Services;

/// <summary>
/// Managed wrapper for Windows Job Objects.
/// Provides OS-level process lifecycle management:
/// - Automatic child process termination when Manager exits.
/// - Immediate crash notification (no polling).
/// - Resource limits (optional, future use).
/// 
/// Requires Windows 10+ for nested Job Object support.
/// </summary>
public sealed class JobObject : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    public JobObject(string? name = null)
    {
        _handle = CreateJobObject(IntPtr.Zero, name);
        if (_handle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create Job Object");

        // Configure: kill all processes in this job when the job handle is closed.
        // This ensures that if Manager dies, all Workers die too.
        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            }
        };

        int infoSize = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
        IntPtr infoPtr = Marshal.AllocHGlobal(infoSize);
        try
        {
            Marshal.StructureToPtr(info, infoPtr, false);
            if (!SetInformationJobObject(_handle, JobObjectInfoType.ExtendedLimitInformation, infoPtr, (uint)infoSize))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to set Job Object limits");
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
        }
    }

    /// <summary>
    /// Assign a process to this Job Object.
    /// The process will be killed when the Job Object is closed (Manager exits).
    /// </summary>
    public bool AssignProcess(Process process)
    {
        if (_disposed || _handle == IntPtr.Zero)
            throw new ObjectDisposedException(nameof(JobObject));

        return AssignProcessToJobObject(_handle, process.Handle);
    }

    /// <summary>
    /// Terminate all processes in this Job Object.
    /// Used for hard-kill during graceful shutdown timeout.
    /// </summary>
    public bool TerminateAll(uint exitCode = 1)
    {
        if (_disposed || _handle == IntPtr.Zero) return false;
        return TerminateJobObject(_handle, exitCode);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }

    // ════════════════════════════════════════════════════════════
    // Win32 P/Invoke
    // ════════════════════════════════════════════════════════════

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    private enum JobObjectInfoType
    {
        BasicLimitInformation = 2,
        ExtendedLimitInformation = 9,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public long Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateJobObject(IntPtr hJob, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
