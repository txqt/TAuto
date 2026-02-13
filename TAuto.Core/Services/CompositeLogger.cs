using System;
using System.Linq;

namespace TAuto.Core.Services;

/// <summary>
/// Fans out log calls to multiple ILoggerService instances.
/// Use to combine file logging with UI relay logging.
/// </summary>
public class CompositeLogger : ILoggerService
{
    private readonly ILoggerService[] _loggers;

    public CompositeLogger(params ILoggerService[] loggers)
    {
        _loggers = loggers ?? throw new ArgumentNullException(nameof(loggers));
    }

    public void Info(string message)
    {
        foreach (var logger in _loggers) logger.Info(message);
    }

    public void Warning(string message)
    {
        foreach (var logger in _loggers) logger.Warning(message);
    }

    public void Error(string message, Exception? ex = null)
    {
        foreach (var logger in _loggers) logger.Error(message, ex);
    }

    public void Fatal(string message, Exception? ex = null)
    {
        foreach (var logger in _loggers) logger.Fatal(message, ex);
    }
}
