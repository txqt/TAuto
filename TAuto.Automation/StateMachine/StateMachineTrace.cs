namespace TAuto.Automation.StateMachine;

/// <summary>
/// A single trace entry in the state machine execution log.
/// </summary>
public class StateMachineTraceEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Event type: StateEnter, StateExit, TransitionCheck, TransitionTrigger, TransitionTimeout, TransitionRetryExceeded
    /// </summary>
    public string EventType { get; set; } = "";
    
    public string StateName { get; set; } = "";
    public string? TransitionTo { get; set; }
    public string? Details { get; set; }
    public int PollCount { get; set; }
    public double ElapsedMs { get; set; }
}

/// <summary>
/// Trace log for debugging state machine execution.
/// </summary>
public class StateMachineTrace
{
    private readonly List<StateMachineTraceEntry> _entries = new();
    private readonly object _lock = new();
    
    /// <summary>
    /// Enable/disable trace logging. Default false for performance.
    /// </summary>
    public bool IsEnabled { get; set; } = false;
    
    /// <summary>
    /// Maximum entries to keep (prevents memory bloat). Default 1000.
    /// </summary>
    public int MaxEntries { get; set; } = 1000;
    
    /// <summary>
    /// Get a copy of all trace entries.
    /// </summary>
    public List<StateMachineTraceEntry> GetEntries()
    {
        lock (_lock)
        {
            return new List<StateMachineTraceEntry>(_entries);
        }
    }
    
    /// <summary>
    /// Log a trace entry.
    /// </summary>
    public void Log(string eventType, string stateName, string? toState = null, string? details = null, int pollCount = 0, double elapsedMs = 0)
    {
        if (!IsEnabled) return;
        
        lock (_lock)
        {
            // Remove oldest if at capacity
            if (_entries.Count >= MaxEntries)
            {
                _entries.RemoveAt(0);
            }
            
            _entries.Add(new StateMachineTraceEntry
            {
                Timestamp = DateTime.UtcNow,
                EventType = eventType,
                StateName = stateName,
                TransitionTo = toState,
                Details = details,
                PollCount = pollCount,
                ElapsedMs = elapsedMs
            });
        }
    }
    
    /// <summary>
    /// Clear all trace entries.
    /// </summary>
    public void Clear()
    {
        lock (_lock) { _entries.Clear(); }
    }
}

/// <summary>
/// Performance metrics for state machine execution.
/// </summary>
public class StateMachineMetrics
{
    private readonly Dictionary<string, StateMetrics> _stateMetrics = new();
    private readonly object _lock = new();
    
    /// <summary>
    /// Record time spent in a state.
    /// </summary>
    public void RecordStateTime(string stateName, double elapsedMs, int pollCount)
    {
        lock (_lock)
        {
            if (!_stateMetrics.TryGetValue(stateName, out var metrics))
            {
                metrics = new StateMetrics { StateName = stateName };
                _stateMetrics[stateName] = metrics;
            }
            
            metrics.TotalTimeMs += elapsedMs;
            metrics.TotalPollCount += pollCount;
            metrics.VisitCount++;
            metrics.AverageTimeMs = metrics.TotalTimeMs / metrics.VisitCount;
        }
    }
    
    /// <summary>
    /// Record a transition occurrence.
    /// </summary>
    public void RecordTransition(string fromState, string toState)
    {
        lock (_lock)
        {
            if (!_stateMetrics.TryGetValue(fromState, out var metrics))
            {
                metrics = new StateMetrics { StateName = fromState };
                _stateMetrics[fromState] = metrics;
            }
            
            if (!metrics.TransitionCounts.ContainsKey(toState))
                metrics.TransitionCounts[toState] = 0;
            
            metrics.TransitionCounts[toState]++;
        }
    }
    
    /// <summary>
    /// Get metrics for all states.
    /// </summary>
    public List<StateMetrics> GetAllMetrics()
    {
        lock (_lock)
        {
            return _stateMetrics.Values.ToList();
        }
    }
    
    /// <summary>
    /// Clear all metrics.
    /// </summary>
    public void Clear()
    {
        lock (_lock) { _stateMetrics.Clear(); }
    }
}

/// <summary>
/// Metrics for a single state.
/// </summary>
public class StateMetrics
{
    public string StateName { get; set; } = "";
    public int VisitCount { get; set; }
    public double TotalTimeMs { get; set; }
    public double AverageTimeMs { get; set; }
    public int TotalPollCount { get; set; }
    public Dictionary<string, int> TransitionCounts { get; } = new();
    
    /// <summary>
    /// Polling efficiency: transitions / polls (higher = better).
    /// </summary>
    public double PollingEfficiency => TotalPollCount > 0 
        ? (double)TransitionCounts.Values.Sum() / TotalPollCount 
        : 0;
}

/// <summary>
/// Validation result for state machine analysis.
/// </summary>
public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// Validates state machine configuration for common issues.
/// </summary>
public static class StateMachineValidator
{
    /// <summary>
    /// Validate a state machine for potential issues.
    /// </summary>
    public static ValidationResult Validate(StateMachine machine)
    {
        var result = new ValidationResult();
        
        if (machine.States.Count == 0)
        {
            result.Errors.Add("State machine has no states.");
            return result;
        }
        
        var stateNames = machine.States.Select(s => s.Name).ToHashSet();
        
        // Check initial state
        if (!string.IsNullOrEmpty(machine.InitialStateName) && !stateNames.Contains(machine.InitialStateName))
        {
            result.Errors.Add($"Initial state '{machine.InitialStateName}' does not exist.");
        }
        
        // Check each state
        foreach (var state in machine.States)
        {
            // Check for unreachable states (no incoming transitions except initial)
            bool isReachable = state.Name == machine.InitialStateName || 
                               (string.IsNullOrEmpty(machine.InitialStateName) && state == machine.States.First());
            
            foreach (var otherState in machine.States)
            {
                if (otherState.Transitions.Any(t => t.ToState == state.Name))
                {
                    isReachable = true;
                    break;
                }
            }
            
            if (!isReachable)
            {
                result.Warnings.Add($"State '{state.Name}' may be unreachable (no incoming transitions).");
            }
            
            // Check for missing transition targets
            foreach (var transition in state.Transitions)
            {
                if (!string.Equals(transition.ToState, "END", StringComparison.OrdinalIgnoreCase) &&
                    !stateNames.Contains(transition.ToState))
                {
                    result.Errors.Add($"State '{state.Name}' has transition to non-existent state '{transition.ToState}'.");
                }
            }
            
            // Check for potential infinite loops (state with no transitions and no timeout)
            if (state.Transitions.Count == 0 && state.MaxDurationMs == 0)
            {
                result.Warnings.Add($"State '{state.Name}' has no transitions and no timeout - may loop forever.");
            }
            
            // Check for unconditional loops
            var unconditionalToSelf = state.Transitions
                .Where(t => t.Condition == null && t.Conditions.Count == 0 && t.ToState == state.Name)
                .ToList();
            
            if (unconditionalToSelf.Any())
            {
                result.Warnings.Add($"State '{state.Name}' has unconditional transition to itself - infinite loop.");
            }
        }
        
        return result;
    }
}

