using System;

namespace TAuto.Core;

/// <summary>
/// Defines a contract for logging message to a persistent store.
/// </summary>
public interface ILoggerService
{
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? ex = null);
    void Fatal(string message, Exception? ex = null);
}
