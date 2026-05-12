using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core.Imaging;

namespace TAuto.Core.Services;

/// <summary>
/// Service responsible for the continuous background screen capture loop.
/// Pushes frames to subscribers via FrameCaptured event.
/// </summary>
public interface IContinuousCaptureService : IDisposable
{
    event EventHandler<IImage>? FrameCaptured;
    
    int CaptureIntervalMs { get; set; }
    int CaptureTimeoutMs { get; set; }
    
    void Start();
    void Stop();
    bool IsRunning { get; }
}

public class ContinuousCaptureService : IContinuousCaptureService
{
    private readonly IDeviceController _device;
    private readonly ScreenCaptureManager _cache;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private readonly object _lock = new();

    public event EventHandler<IImage>? FrameCaptured;

    public int CaptureIntervalMs { get; set; } = 100;
    public int CaptureTimeoutMs { get; set; } = 5000;

    public bool IsRunning => _cts != null;

    public ContinuousCaptureService(IDeviceController device, ScreenCaptureManager cache)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        
        // Sync settings from cache initially
        CaptureIntervalMs = cache.CaptureIntervalMs;
        CaptureTimeoutMs = cache.CaptureTimeoutMs;
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => CaptureLoopAsync(_cts.Token));
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_cts == null) return;
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    private async Task CaptureLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var interval = TimeSpan.FromMilliseconds(Math.Max(1, CaptureIntervalMs));
                using var timer = new PeriodicTimer(interval);

                while (await timer.WaitForNextTickAsync(ct))
                {
                    if (string.IsNullOrEmpty(_device.TargetId))
                        continue;

                    // Pull-based update of the shared cache (forces a refresh)
                    // We sync timeout settings before capture
                    _cache.CaptureTimeoutMs = CaptureTimeoutMs;
                    var success = await _cache.UpdateScreenCaptureAsync(force: true);

                    if (success && _cache.LastScreenCapture != null)
                    {
                        IImage? frameClone = null;
                        try
                        {
                            frameClone = _cache.LastScreenCapture.Clone();
                            FrameCaptured?.Invoke(this, frameClone);
                            frameClone = null; // Ownership transferred to subscriber
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ContinuousCaptureService] FrameCaptured handler error: {ex.Message}");
                        }
                        finally
                        {
                            frameClone?.Dispose(); // Dispose only if NOT consumed
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ContinuousCaptureService] Loop error: {ex.Message}");
                await Task.Delay(1000, ct); // Cool down on error
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
