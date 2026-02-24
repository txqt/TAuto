using System;

namespace TAuto.Core.Services;

public interface ICrashLoopProtector
{
    bool RegisterCrashAndCheckIfLooping(string workerId);
    void ClearHistory(string workerId);
}
