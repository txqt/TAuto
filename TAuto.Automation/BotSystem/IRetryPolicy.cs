using System;
using System.Threading;
using System.Threading.Tasks;

namespace TAuto.Automation.BotSystem;

public interface IRetryPolicy
{
    Task<bool> RetryAsync(Func<Task<bool>> action, int maxRetries = 3, int intervalMs = 1000, CancellationToken ct = default);
}

public class DefaultRetryPolicy : IRetryPolicy
{
    public async Task<bool> RetryAsync(Func<Task<bool>> action, int maxRetries = 3, int intervalMs = 1000, CancellationToken ct = default)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (await action()) return true;
            try { await Task.Delay(intervalMs, ct); } catch (TaskCanceledException) { throw; }
        }
        return false;
    }
}
