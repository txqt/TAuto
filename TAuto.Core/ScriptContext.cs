using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core.Imaging;
using TAuto.Core.Models;

namespace TAuto.Core;

/// <summary>
/// Execution context for scripts.
/// Acts as a Facade over ScriptState and ScreenCaptureManager.
/// </summary>
public class ScriptContext : IDisposable
{
    private readonly ScriptState _state = new();
    private readonly ScreenCaptureManager _captureManager;
    
    /// <summary>
    /// State-scoped local variables. Key = StateName, Value = local variable dictionary.
    /// Thread-safe: uses ConcurrentDictionary for concurrent access.
    /// </summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object>> _scopedVariables = new();
    
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
    
    /// <summary>Bot personality profile (loaded from disk, unique per bot).</summary>
    public BotPersona? Persona { get; set; }
    
    /// <summary>Persistent session data (loaded from disk, saved on shutdown).</summary>
    public BotSession? Session { get; set; }
    
    /// <summary>Optional health monitor for the game being automated.</summary>
    public GameHealthMonitor? HealthMonitor { get; set; }
    
    /// <summary>Short-term memory of recent actions and outcomes.</summary>
    public EpisodicMemory Memory { get; } = new();
    
    public IImage? LastScreenCapture => _captureManager.LastScreenCapture;
    public DateTime? LastCaptureTime => _captureManager.LastCaptureTime;
    
    public int CaptureIntervalMs 
    { 
        get => _captureManager.CaptureIntervalMs;
        set => _captureManager.CaptureIntervalMs = value;
    }

    /// <summary>
    /// Hard timeout (ms) for screen capture. Prevents hanging if target window freezes.
    /// Default 5000ms. Passthrough to ScreenCaptureManager.
    /// </summary>
    public int CaptureTimeoutMs
    {
        get => _captureManager.CaptureTimeoutMs;
        set => _captureManager.CaptureTimeoutMs = value;
    }

    public System.Drawing.Point? LastFoundImageLocation 
    { 
        get => _captureManager.LastFoundImageLocation;
        set => _captureManager.LastFoundImageLocation = value;
    }
    
    /// <summary>
    /// Target action ID to jump to. Set by logic actions (If/Goto), consumed by ScriptRunner/StateMachine.
    /// </summary>
    public string? JumpToId { get; set; }
    
    /// <summary>
    /// When true, the StateMachine uses faster polling intervals for sub-100ms reaction.
    /// Set by actions/bots during time-critical phases (combat, alerts).
    /// Default: false.
    /// </summary>
    public bool IsUrgentMode { get; set; } = false;
    
    public ILoggerService? Logger { get; }

    public ScriptContext(IDeviceController device, IVisionService vision, IOcrService ocr, ILoggerService? logger = null)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
        Vision = vision ?? throw new ArgumentNullException(nameof(vision));
        Ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        Logger = logger;

        _captureManager = new ScreenCaptureManager(Device);
    }

    public void StartCaptureLoop() => _captureManager.StartCaptureLoop();
    public void StopCaptureLoop() => _captureManager.StopCaptureLoop();
    public event EventHandler<IImage>? FrameCaptured
    {
        add => _captureManager.FrameCaptured += value;
        remove => _captureManager.FrameCaptured -= value;
    }

    private readonly Dictionary<(string, double), TemplateMatchResult> _visionCache = new();

    public void CacheMatch(string templatePath, double threshold, TemplateMatchResult result)
    {
        _visionCache[(templatePath, threshold)] = result;
    }

    public TemplateMatchResult? GetCachedMatch(string templatePath, double threshold)
    {
        if (_visionCache.TryGetValue((templatePath, threshold), out var result))
            return result;
        return null;
    }

    public async Task<bool> UpdateScreenCaptureAsync(bool force = false)
    {
        bool result = await _captureManager.UpdateScreenCaptureAsync(force);
        if (force || result)
        {
            _visionCache.Clear();
        }
        HealthMonitor?.ReportCaptureResult(_captureManager.LastScreenCapture);
        return result;
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
    
    #region Scoped Local Variables
    
    /// <summary>
    /// Set a variable scoped to a specific state. Cleared when state exits.
    /// </summary>
    public void SetLocalVariable(string stateName, string key, object value)
    {
        var scope = _scopedVariables.GetOrAdd(stateName, _ => new ConcurrentDictionary<string, object>());
        scope[key] = value;
    }
    
    /// <summary>
    /// Get a variable scoped to a specific state.
    /// </summary>
    public T GetLocalVariable<T>(string stateName, string key, T defaultValue = default!)
    {
        if (_scopedVariables.TryGetValue(stateName, out var scope) && scope.TryGetValue(key, out var value))
        {
            if (value is T typedValue) return typedValue;
            try { return (T)Convert.ChangeType(value, typeof(T)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ScriptContext] GetVar<{typeof(T).Name}>('{key}') cast failed: {ex.Message}"); return defaultValue; }
        }
        return defaultValue;
    }
    
    /// <summary>
    /// Clear all local variables for a specific state. Called on state exit.
    /// </summary>
    public void ClearLocalVariables(string stateName)
    {
        _scopedVariables.TryRemove(stateName, out _);
    }
    
    #endregion
    
    #region Event System
    public SemaphoreSlim EventSignal => _state.EventSignal;
    public void RaiseEvent(string name) => _state.RaiseEvent(name);
    public bool ConsumeEvent(string name) => _state.ConsumeEvent(name);
    public void ClearEvents() => _state.ClearEvents();
    public bool HasEvent(string name) => _state.HasEvent(name);
    #endregion
    public void Dispose()
    {
        _captureManager?.Dispose();
        _visionCache.Clear();
        _scopedVariables.Clear();
    }
}

