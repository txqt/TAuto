namespace TAuto.Shared.Ipc;

/// <summary>
/// Abstracts resource governance for CPU-heavy operations.
///
/// In-process mode: Always grants tokens (no-op).
/// Worker mode: Requests tokens from Manager via IPC.
///
/// Used by Vision/OCR services to gate expensive operations.
/// </summary>
public interface IResourceGovernor
{
    /// <summary>
    /// Acquire a compute token before performing a heavy operation.
    /// Returns true if granted, false if denied/timed out.
    /// </summary>
    Task<bool> AcquireTokenAsync(int timeoutMs = 5000, CancellationToken ct = default);

    /// <summary>
    /// Release a previously acquired compute token.
    /// </summary>
    void ReleaseToken();
}

/// <summary>
/// No-op governor for in-process (monolithic) mode.
/// Always grants tokens immediately — zero overhead.
/// </summary>
public class PassthroughResourceGovernor : IResourceGovernor
{
    public static readonly PassthroughResourceGovernor Instance = new();

    public Task<bool> AcquireTokenAsync(int timeoutMs = 5000, CancellationToken ct = default)
        => Task.FromResult(true);

    public void ReleaseToken() { }
}
