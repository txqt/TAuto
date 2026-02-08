using System;
using System.Collections.Generic;
using System.Threading;

namespace TAuto.Core;

/// <summary>
/// Manages the execution state of a script, including variables and events.
/// </summary>
public class ScriptState
{
    private readonly Dictionary<string, object> _variables = new();
    private readonly object _lock = new();
    
    // Event for UI to watch variables
    public event EventHandler<VariableChangedEventArgs>? VariableChanged;

    #region Event System
    private readonly HashSet<string> _raisedEvents = new();
    private readonly SemaphoreSlim _eventSignal = new(0); 

    public SemaphoreSlim EventSignal => _eventSignal;
    #endregion

    public T GetVariable<T>(string name, T defaultValue = default!)
    {
        lock (_lock)
        {
            if (_variables.TryGetValue(name, out var value))
            {
                if (value is T typedValue) return typedValue;
                try { return (T)Convert.ChangeType(value, typeof(T)); }
                catch { return defaultValue; }
            }
            return defaultValue;
        }
    }

    public void SetVariable(string name, object value)
    {
        lock (_lock)
        {
            _variables[name] = value;
        }
        VariableChanged?.Invoke(this, new VariableChangedEventArgs(name, value));
    }

    public bool HasVariable(string name)
    {
        lock (_lock)
        {
            return _variables.ContainsKey(name);
        }
    }

    public bool RemoveVariable(string name)
    {
        bool removed;
        lock (_lock)
        {
            removed = _variables.Remove(name);
        }
        
        if (removed)
            VariableChanged?.Invoke(this, new VariableChangedEventArgs(name, null));
        return removed;
    }

    public void ClearVariables()
    {
        lock (_lock)
        {
            _variables.Clear();
        }
        VariableChanged?.Invoke(this, new VariableChangedEventArgs("*", null));
    }

    public IEnumerable<string> GetVariableNames() 
    {
        lock (_lock)
        {
            return new List<string>(_variables.Keys); 
        }
    }
    
    public Dictionary<string, object> GetAllVariables()
    {
        lock (_lock)
        {
            return new Dictionary<string, object>(_variables);
        }
    }

    public void RaiseEvent(string name) 
    { 
        lock (_lock) { _raisedEvents.Add(name); }
        try { _eventSignal.Release(); } catch (SemaphoreFullException) { /* ignore */ }
    }

    public bool ConsumeEvent(string name) 
    { 
        lock (_lock) { return _raisedEvents.Remove(name); } 
    }

    public void ClearEvents() 
    { 
        lock (_lock) { _raisedEvents.Clear(); } 
    }

    public bool HasEvent(string name)
    {
        lock (_lock) { return _raisedEvents.Contains(name); }
    }
}
