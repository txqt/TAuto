using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace TAuto.Core.Services;

public class DefaultCrashLoopProtector : ICrashLoopProtector
{
    private readonly ConcurrentDictionary<string, List<DateTime>> _crashHistory = new();
    public int MaxCrashesBeforeStop { get; set; } = AutomationDefaults.DefaultMaxCrashesBeforeStop;
    public TimeSpan CrashWindowDuration { get; set; } = TimeSpan.FromSeconds(AutomationDefaults.DefaultCrashWindowSeconds);

    public bool RegisterCrashAndCheckIfLooping(string workerId)
    {
        var history = _crashHistory.GetOrAdd(workerId, _ => new List<DateTime>());
        lock (history)
        {
            var now = DateTime.UtcNow;
            history.RemoveAll(t => (now - t) > CrashWindowDuration);
            history.Add(now);

            return history.Count > MaxCrashesBeforeStop;
        }
    }

    public void ClearHistory(string workerId)
    {
        _crashHistory.TryRemove(workerId, out _);
    }
}
