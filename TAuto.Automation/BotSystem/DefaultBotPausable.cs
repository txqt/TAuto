using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TAuto.Automation.BotSystem;

public class DefaultBotPausable : IBotPausable
{
    private volatile TaskCompletionSource<bool>? _pauseSignal;
    private readonly Func<ILogger?> _loggerProvider;

    public DefaultBotPausable(Func<ILogger?> loggerProvider)
    {
        _loggerProvider = loggerProvider ?? throw new ArgumentNullException(nameof(loggerProvider));
    }

    public event Action<bool>? OnPausedStateChanged;

    public bool IsPaused => _pauseSignal != null;

    public void Pause()
    {
        if (_pauseSignal == null || _pauseSignal.Task.IsCompleted)
        {
            _pauseSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            OnPausedStateChanged?.Invoke(true);
            _loggerProvider()?.LogInformation("Bot paused.");
        }
    }

    public void Resume()
    {
        if (_pauseSignal != null)
        {
            _pauseSignal.TrySetResult(true);
            _pauseSignal = null;
            OnPausedStateChanged?.Invoke(false);
            _loggerProvider()?.LogInformation("Bot resumed.");
        }
    }

    public async Task CheckPausedAsync()
    {
        if (_pauseSignal != null)
        {
            await _pauseSignal.Task;
        }
    }
}
