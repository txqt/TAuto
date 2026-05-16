using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TAuto.Core;

/// <summary>
/// Manages the execution state of a script, including variables and events.
/// Thread-safe: uses ConcurrentDictionary for lock-free reads (high-frequency polling).
/// </summary>
public class ScriptState
{
    private readonly ILoggerService? _logger;
    private readonly ConcurrentDictionary<string, object> _variables = new();
    private readonly ConcurrentDictionary<string, byte> _raisedEvents = new();
    private readonly SemaphoreSlim _eventSignal = new(0);

    public ScriptState(ILoggerService? logger = null)
    {
        _logger = logger;
    }

    // Event for UI to watch variables
    public event EventHandler<VariableChangedEventArgs>? VariableChanged;

    public SemaphoreSlim EventSignal => _eventSignal;

    #region Variables (Lock-Free)

    public T GetVariable<T>(string name, T defaultValue = default!)
    {
        if (_variables.TryGetValue(name, out var value))
        {
            if (value is T typedValue) return typedValue;
            try { return (T)Convert.ChangeType(value, typeof(T)); }
            catch (Exception ex) 
            { 
                _logger?.Warning($"[ScriptState] GetVar<{typeof(T).Name}>('{name}') cast failed from type {value?.GetType().Name}: {ex.Message}");
                return defaultValue; 
            }
        }
        return defaultValue;
    }

    public void SetVariable(string name, object value)
    {
        _variables[name] = value;
        VariableChanged?.Invoke(this, new VariableChangedEventArgs(name, value));
    }

    public bool HasVariable(string name) => _variables.ContainsKey(name);

    public bool RemoveVariable(string name)
    {
        bool removed = _variables.TryRemove(name, out _);
        if (removed)
            VariableChanged?.Invoke(this, new VariableChangedEventArgs(name, null));
        return removed;
    }

    public void ClearVariables()
    {
        _variables.Clear();
        VariableChanged?.Invoke(this, new VariableChangedEventArgs("*", null));
    }

    public IEnumerable<string> GetVariableNames() => _variables.Keys.ToList();

    public Dictionary<string, object> GetAllVariables() => new(_variables);

    #endregion

    #region Event System (Lock-Free)

    public void RaiseEvent(string name)
    {
        _raisedEvents.TryAdd(name, 0);
        try { _eventSignal.Release(); } catch (SemaphoreFullException) { /* ignore */ }
    }

    public bool ConsumeEvent(string name) => _raisedEvents.TryRemove(name, out _);

    public void ClearEvents() => _raisedEvents.Clear();

    public bool HasEvent(string name) => _raisedEvents.ContainsKey(name);

    #endregion
}
