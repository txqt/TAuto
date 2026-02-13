using System;
using System.IO;

namespace TAuto.Core.Services;

/// <summary>
/// Writes logs to a daily file in AppData.
/// Thread-safe via lock.
/// </summary>
public class FileLoggerService : ILoggerService
{
    private readonly string _logDirectory;
    private readonly object _lock = new();

    public FileLoggerService() : this("AutoBot") { }

    public FileLoggerService(string appName)
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _logDirectory = Path.Combine(appData, appName, "Logs");

        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    public void Info(string message) => WriteLog("INFO", message);
    public void Warning(string message) => WriteLog("WARN", message);
    public void Error(string message, Exception? ex = null) => WriteLog("ERROR", message, ex);
    public void Fatal(string message, Exception? ex = null) => WriteLog("FATAL", message, ex);

    private void WriteLog(string level, string message, Exception? ex = null)
    {
        try
        {
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            string filename = $"autobot_{date}.log";
            string path = Path.Combine(_logDirectory, filename);
            
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string logLine = $"[{timestamp}] [{level}] {message}";

            if (ex != null)
            {
                logLine += Environment.NewLine + ex.ToString();
            }

            lock (_lock)
            {
                File.AppendAllText(path, logLine + Environment.NewLine);
            }
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"Failed to write log: {message}");
        }
    }
}
