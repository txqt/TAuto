using System;

namespace TAuto.Core.Services;

/// <summary>
/// Logger that relays messages to a callback action.
/// Used for UI-bound logging (ViewModel, Console, etc).
/// </summary>
public class RelayLogger : ILoggerService
{
    private readonly Action<string> _logAction;

    public RelayLogger(Action<string> logAction)
    {
        _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
    }

    public void Info(string message) => _logAction(message);
    public void Warning(string message) => _logAction($"⚠ {message}");
    public void Error(string message, Exception? ex = null) => _logAction($"❌ {message}" + (ex != null ? $": {ex.Message}" : ""));
    public void Fatal(string message, Exception? ex = null) => _logAction($"💀 {message}" + (ex != null ? $": {ex.Message}" : ""));
}
