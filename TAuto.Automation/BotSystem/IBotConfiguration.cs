using System;
using System.Collections.Generic;

namespace TAuto.Automation.BotSystem;

public interface IBotConfiguration
{
    Dictionary<string, object> Arguments { get; }
    void SetArguments(Dictionary<string, object> args);
    T GetArg<T>(string name, T defaultValue = default!);
    string GetArgString(string name, string defaultValue = "");
    int GetArgInt(string name, int defaultValue = 0);
    bool GetArgBool(string name, bool defaultValue = false);
    double GetArgDouble(string name, double defaultValue = 0.0);
    event Action<string>? OnArgumentFallback;
}

public class DefaultBotConfiguration : IBotConfiguration
{
    public Dictionary<string, object> Arguments { get; private set; } = new();

    public void SetArguments(Dictionary<string, object> args)
    {
        Arguments = args ?? new();
    }

    public event Action<string>? OnArgumentFallback;

    public T GetArg<T>(string name, T defaultValue = default!)
    {
        if (Arguments.TryGetValue(name, out var value))
        {
            if (value is System.Text.Json.JsonElement jsonElement)
            {
                try
                {
                    if (typeof(T) == typeof(string)) return (T)(object)(jsonElement.GetString() ?? "");
                    if (typeof(T) == typeof(int)) return (T)(object)jsonElement.GetInt32();
                    if (typeof(T) == typeof(bool)) return (T)(object)jsonElement.GetBoolean();
                    if (typeof(T) == typeof(double)) return (T)(object)jsonElement.GetDouble();
                }
                catch { }
            }

            try { return (T)Convert.ChangeType(value, typeof(T)); }
            catch (Exception ex) 
            {
                string msg = $"[Warning] Argument '{name}' failed to parse. Falling back to default: '{defaultValue}'. Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(msg);
                OnArgumentFallback?.Invoke(msg);
                return defaultValue; 
            }
        }
        
        string missingMsg = $"[Warning] Argument '{name}' is missing. Falling back to default: '{defaultValue}'.";
        System.Diagnostics.Debug.WriteLine(missingMsg);
        OnArgumentFallback?.Invoke(missingMsg);

        return defaultValue;
    }

    public string GetArgString(string name, string defaultValue = "") => GetArg(name, defaultValue);
    public int GetArgInt(string name, int defaultValue = 0) => GetArg(name, defaultValue);
    public bool GetArgBool(string name, bool defaultValue = false) => GetArg(name, defaultValue);
    public double GetArgDouble(string name, double defaultValue = 0.0) => GetArg(name, defaultValue);
}
