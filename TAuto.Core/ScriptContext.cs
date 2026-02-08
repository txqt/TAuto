using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TAuto.Core;

/// <summary>
/// Event args for variable changes.
/// </summary>
public class VariableChangedEventArgs : EventArgs
{
    public string VariableName { get; }
    public object? NewValue { get; }
    public VariableChangedEventArgs(string name, object? value) { VariableName = name; NewValue = value; }
}

/// <summary>
/// Execution context for scripts.
/// </summary>
public class ScriptContext
{
    private readonly Dictionary<string, object> _variables = new();
    private readonly object _lock = new(); // Thread safety lock
    
    // Event for UI to watch variables
    public event EventHandler<VariableChangedEventArgs>? VariableChanged;
    
    public IDeviceController Device { get; }
    public IVisionService Vision { get; }
    public IOcrService Ocr { get; }
    
    public string TargetId 
    { 
        get => Device.TargetId; 
        set => Device.TargetId = value; 
    }
    
    public Guid SessionId { get; } = Guid.NewGuid(); // Unique session ID
    public BitmapSource? LastScreenCapture { get; private set; }
    public DateTime? LastCaptureTime { get; private set; }
    public int CaptureIntervalMs { get; set; } = 100;
    public System.Windows.Point? LastFoundImageLocation { get; set; }
    
    /// <summary>
    /// Target action ID to jump to. Set by logic actions (If/Goto), consumed by ScriptRunner/StateMachine.
    /// </summary>
    public string? JumpToId { get; set; }
    
    public ILoggerService? Logger { get; }
    
    public ScriptContext(IDeviceController device, IVisionService vision, IOcrService ocr, ILoggerService? logger = null)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
        Vision = vision ?? throw new ArgumentNullException(nameof(vision));
        Ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        Logger = logger;
    }
    
    public async Task<bool> UpdateScreenCaptureAsync(bool force = false)
    {
        if (string.IsNullOrEmpty(TargetId))
            return false;
            
        if (!force && LastScreenCapture != null && LastCaptureTime.HasValue)
        {
            var elapsed = (DateTime.Now - LastCaptureTime.Value).TotalMilliseconds;
            if (elapsed < CaptureIntervalMs)
                return true;
        }
        
        var capture = await Device.CaptureScreenAsync();
        if (capture != null)
        {
            LastScreenCapture = capture;
            LastCaptureTime = DateTime.Now;
            return true;
        }
        
        return false;
    }
    
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
    
    public int GetInt(string name, int defaultValue = 0) => GetVariable(name, defaultValue);
    public bool GetBool(string name, bool defaultValue = false) => GetVariable(name, defaultValue);
    public string GetString(string name, string defaultValue = "") => GetVariable(name, defaultValue);
    public double GetDouble(string name, double defaultValue = 0.0) => GetVariable(name, defaultValue);
    
    public void SetVariable(string name, object value)
    {
        lock (_lock)
        {
            _variables[name] = value;
        }
        VariableChanged?.Invoke(this, new VariableChangedEventArgs(name, value));
    }
    
    public int Increment(string name, int amount = 1)
    {
        var current = GetInt(name, 0);
        var newValue = current + amount;
        SetVariable(name, newValue); // Invokes event
        return newValue;
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
            return new List<string>(_variables.Keys); // Return copy
        }
    }
    
    public Dictionary<string, object> GetAllVariables()
    {
        lock (_lock)
        {
            return new Dictionary<string, object>(_variables);
        }
    }
    
    #region Event System
    private readonly HashSet<string> _raisedEvents = new();
    private readonly SemaphoreSlim _eventSignal = new(0); // For wake-up

    /// <summary>
    /// Signaled when any event is raised. Used to wake up polling loops.
    /// </summary>
    public SemaphoreSlim EventSignal => _eventSignal;

    /// <summary>
    /// Raise an event that can be consumed by EventTransition.
    /// </summary>
    public void RaiseEvent(string name) 
    { 
        lock (_lock) { _raisedEvents.Add(name); }
        // Release to wake any waiting StateMachine loops
        try { _eventSignal.Release(); } catch (SemaphoreFullException) { /* ignore */ }
    }

    /// <summary>
    /// Consume an event (removes it from the set). Returns true if event existed.
    /// </summary>
    public bool ConsumeEvent(string name) 
    { 
        lock (_lock) { return _raisedEvents.Remove(name); } 
    }

    /// <summary>
    /// Clear all raised events.
    /// </summary>
    public void ClearEvents() 
    { 
        lock (_lock) { _raisedEvents.Clear(); } 
    }

    /// <summary>
    /// Check if an event is currently raised (without consuming).
    /// </summary>
    public bool HasEvent(string name)
    {
        lock (_lock) { return _raisedEvents.Contains(name); }
    }
    #endregion
}
