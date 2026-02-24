using System;

namespace TAuto.Core.Services;

public interface ILogStreamer : IDisposable
{
    void WriteLog(string workerId, string level, string message);
    void WriteStatus(string workerId, string status);
    void CloseWriter(string workerId);
}
