using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TAuto.Core;

/// <summary>
/// Execution context for scripts.
/// Acts as a Facade over ScriptState and ScreenCaptureManager.
/// </summary>
public class ScriptContext
{
    private readonly ScriptState _state = new();
    private readonly ScreenCaptureManager _captureManager;
    
    // Event for UI to watch variables
    public event EventHandler<VariableChangedEventArgs>? VariableChanged
    {
        add => _state.VariableChanged += value;
        remove => _state.VariableChanged -= value;
    }
    
    public IDeviceController Device { get; }
    public IVisionService Vision { get; }
    public IOcrService Ocr { get; }
    
    public string TargetId 
    { 
        get => Device.TargetId; 
        set => Device.TargetId = value; 
    }
    
    public Guid SessionId { get; } = Guid.NewGuid(); // Unique session ID
    
    public BitmapSource? LastScreenCapture => _captureManager.LastScreenCapture;
    public DateTime? LastCaptureTime => _captureManager.LastCaptureTime;
    
    public int CaptureIntervalMs 
    { 
        get => _captureManager.CaptureIntervalMs;
        set => _captureManager.CaptureIntervalMs = value;
    }

    public System.Windows.Point? LastFoundImageLocation 
    { 
        get => _captureManager.LastFoundImageLocation;
        set => _captureManager.LastFoundImageLocation = value;
    }
    
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
        
        _captureManager = new ScreenCaptureManager(Device);
    }
    
    public Task<bool> UpdateScreenCaptureAsync(bool force = false)
    {
        return _captureManager.UpdateScreenCaptureAsync(force);
    }
    
    public T GetVariable<T>(string name, T defaultValue = default!) => _state.GetVariable(name, defaultValue);
    public int GetInt(string name, int defaultValue = 0) => _state.GetVariable(name, defaultValue);
    public bool GetBool(string name, bool defaultValue = false) => _state.GetVariable(name, defaultValue);
    public string GetString(string name, string defaultValue = "") => _state.GetVariable(name, defaultValue);
    public double GetDouble(string name, double defaultValue = 0.0) => _state.GetVariable(name, defaultValue);
    public void SetVariable(string name, object value) => _state.SetVariable(name, value);
    public int Increment(string name, int amount = 1)
    {
        var current = GetInt(name, 0);
        var newValue = current + amount;
        SetVariable(name, newValue);
        return newValue;
    }
    public bool HasVariable(string name) => _state.HasVariable(name);
    public bool RemoveVariable(string name) => _state.RemoveVariable(name);
    public void ClearVariables() => _state.ClearVariables();
    public IEnumerable<string> GetVariableNames() => _state.GetVariableNames();
    public Dictionary<string, object> GetAllVariables() => _state.GetAllVariables();
    
    #region Event System
    public SemaphoreSlim EventSignal => _state.EventSignal;
    public void RaiseEvent(string name) => _state.RaiseEvent(name);
    public bool ConsumeEvent(string name) => _state.ConsumeEvent(name);
    public void ClearEvents() => _state.ClearEvents();
    public bool HasEvent(string name) => _state.HasEvent(name);
    #endregion
}
