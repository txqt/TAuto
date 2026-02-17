using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace TAuto.Core.Services;

/// <summary>
/// Centralized resource governor for CPU-heavy operations (OCR, Multi-Scale Matching).
/// Uses a SemaphoreSlim to limit concurrent heavy ops across all Workers.
/// Tracks tokens per Worker to prevent leaks on crash/disconnect.
///
/// Default permit count: Environment.ProcessorCount - 2 (leave headroom for Manager + OS).
/// </summary>
public class ComputeTokenService : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxTokens;
    private readonly ConcurrentDictionary<string, int> _workerTokens = new();
    private int _activeTokens;
    private bool _disposed;

    /// <summary>
    /// Number of tokens currently held by Workers.
    /// </summary>
    public int ActiveTokens => _activeTokens;

    /// <summary>
    /// Maximum concurrent tokens available.
    /// </summary>
    public int MaxTokens => _maxTokens;

    public ComputeTokenService(int? maxTokens = null)
    {
        _maxTokens = maxTokens ?? Math.Max(1, Environment.ProcessorCount - 2);
        _semaphore = new SemaphoreSlim(_maxTokens, _maxTokens);
    }

    /// <summary>
    /// Try to acquire a compute token for a specific Worker within the given timeout.
    /// Returns true if a token was granted, false if timed out.
    /// </summary>
    public async Task<bool> TryAcquireAsync(string workerId, int timeoutMs = 5000, CancellationToken ct = default)
    {
        var acquired = await _semaphore.WaitAsync(timeoutMs, ct);
        if (acquired)
        {
            Interlocked.Increment(ref _activeTokens);
            _workerTokens.AddOrUpdate(workerId, 1, (_, count) => count + 1);
        }
        return acquired;
    }

    /// <summary>
    /// Release a previously acquired compute token for a specific Worker.
    /// </summary>
    public void Release(string workerId)
    {
        _semaphore.Release();
        Interlocked.Decrement(ref _activeTokens);
        _workerTokens.AddOrUpdate(workerId, 0, (_, count) => Math.Max(0, count - 1));
    }

    /// <summary>
    /// Force-release ALL tokens held by a specific Worker.
    /// Called when a Worker crashes or disconnects to prevent token leaks.
    /// </summary>
    public void ReleaseAllForWorker(string workerId)
    {
        if (_workerTokens.TryRemove(workerId, out var count) && count > 0)
        {
            for (int i = 0; i < count; i++)
            {
                try
                {
                    _semaphore.Release();
                    Interlocked.Decrement(ref _activeTokens);
                }
                catch (SemaphoreFullException)
                {
                    // Safety: don't over-release
                    break;
                }
            }
            System.Diagnostics.Debug.WriteLine(
                $"[ComputeTokenService] Force-released {count} token(s) for crashed worker '{workerId}'");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _semaphore.Dispose();
    }
}
