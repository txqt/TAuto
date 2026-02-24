using System.Collections.ObjectModel;
using TAuto.Automation.Actions;
using TAuto.Core;

namespace TAuto.Automation.StateMachine;

/// <summary>
/// Fluent builder for constructing StateMachine instances.
/// Produces the same StateMachine object graph but with a readable, chainable API.
/// 
/// Usage:
///   var sm = new StateMachineBuilder()
///       .StartAt("CheckCity")
///       .State("CheckCity")
///           .Log("Checking City View...")
///           .TransitionTo("GoToMap", When.ImageFound("map_button.png"), priority: 10)
///           .TransitionTo("OpenSearch")
///       .State("GoToMap")
///           .PressKey("Space")
///           .Delay(1500)
///           .TransitionTo("OpenSearch")
///       .Build();
/// </summary>
public class StateMachineBuilder
{
    private readonly StateMachine _machine = new();
    private State? _currentState;
    private readonly List<StateTransition> _globalTransitions = new();

    // ════════════════════════════════════════════════════════════
    // Machine-Level Configuration
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Set the initial state name.
    /// </summary>
    public StateMachineBuilder StartAt(string stateName)
    {
        _machine.InitialStateName = stateName;
        return this;
    }

    /// <summary>
    /// Set the maximum number of transitions before aborting (infinite loop guard).
    /// </summary>
    public StateMachineBuilder MaxTransitions(int max)
    {
        _machine.LoopMonitor.MaxTransitions = max;
        return this;
    }


    // ════════════════════════════════════════════════════════════
    // State Definition
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Begin defining a new state. Closes the previous state if any.
    /// </summary>
    public StateMachineBuilder State(string name)
    {
        FinalizeCurrentState();

        _currentState = new State { Name = name };
        return this;
    }



    /// <summary>
    /// Set the maximum time (ms) to stay in this state before failing.
    /// </summary>
    public StateMachineBuilder MaxDuration(int ms)
    {
        EnsureState();
        _currentState!.MaxDurationMs = ms;
        return this;
    }

    /// <summary>
    /// Configure adaptive polling intervals for this state.
    /// </summary>
    public StateMachineBuilder PollingIntervals(int fastMs = 50, int slowMs = 500, int slowdownThreshold = 3)
    {
        EnsureState();
        _currentState!.FastCheckIntervalMs = fastMs;
        _currentState!.SlowCheckIntervalMs = slowMs;
        _currentState!.SlowdownThreshold = slowdownThreshold;
        return this;
    }

    // ════════════════════════════════════════════════════════════
    // Entry Actions (executed when entering the state)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Add a log message as an entry action.
    /// </summary>
    public StateMachineBuilder Log(string message)
    {
        EnsureState();
        _currentState!.EntryActions.Add(new LogAction { Message = message });
        return this;
    }

    /// <summary>
    /// Add a delay as an entry action.
    /// </summary>
    public StateMachineBuilder Delay(int ms)
    {
        EnsureState();
        _currentState!.EntryActions.Add(new DelayAction { DelayMs = ms });
        return this;
    }

    /// <summary>
    /// Add a key press as an entry action.
    /// </summary>
    public StateMachineBuilder PressKey(string key)
    {
        EnsureState();
        _currentState!.EntryActions.Add(new PressKeyAction { Key = key });
        return this;
    }

    /// <summary>
    /// Add a screen tap as an entry action.
    /// </summary>
    public StateMachineBuilder Tap(int x, int y)
    {
        EnsureState();
        _currentState!.EntryActions.Add(new TapAction { X = x, Y = y });
        return this;
    }

    /// <summary>
    /// Add a scaled screen tap as an entry action.
    /// Assumes coordinates are based on 1280x720 and scales them to the device resolution.
    /// </summary>
    public StateMachineBuilder TapScaled(int x, int y)
    {
        EnsureState();
        _currentState!.EntryActions.Add(new TapAction { X = x, Y = y, UseScaling = true });
        return this;
    }

    /// <summary>
    /// Add an image click as an entry action.
    /// </summary>
    public StateMachineBuilder ClickImage(string templatePath, int delayAfterMs = 0, int timeoutMs = 10000)
    {
        EnsureState();
        _currentState!.EntryActions.Add(new ClickImageAction
        {
            TemplatePath = templatePath,
            DelayAfterMs = delayAfterMs,
            TimeoutMs = timeoutMs
        });
        return this;
    }

    /// <summary>
    /// Add OCR text extraction as an entry action.
    /// </summary>
    public StateMachineBuilder ExtractText(int x, int y, int width, int height, string outputVariable,
        double scale = 4.0, string? whitelist = null, int threshold = 150, int pageSegMode = 7)
    {
        EnsureState();
        _currentState!.EntryActions.Add(new ExtractTextAction
        {
            X = x, Y = y, Width = width, Height = height,
            OutputVariable = outputVariable,
            Scale = scale,
            Whitelist = whitelist ?? "",
            Threshold = threshold,
            PageSegMode = pageSegMode
        });
        return this;
    }

    /// <summary>
    /// Add any IAction as an entry action.
    /// </summary>
    public StateMachineBuilder Action(IAction action)
    {
        EnsureState();
        _currentState!.EntryActions.Add(action);
        return this;
    }

    /// <summary>
    /// Add an inline delegate as an entry action.
    /// </summary>
    public StateMachineBuilder Action(Func<ScriptContext, CancellationToken, Task<ActionResult>> action)
    {
        EnsureState();
        _currentState!.EntryActions.Add(new DelegateAction(action));
        return this;
    }

    /// <summary>
    /// Add a synchronous inline delegate as an entry action.
    /// </summary>
    public StateMachineBuilder Action(Action<ScriptContext> action)
    {
        EnsureState();
        _currentState!.EntryActions.Add(new DelegateAction(action));
        return this;
    }

    // ════════════════════════════════════════════════════════════
    // Exit Actions
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Add an action to execute when exiting this state.
    /// </summary>
    public StateMachineBuilder OnExit(IAction action)
    {
        EnsureState();
        _currentState!.ExitActions.Add(action);
        return this;
    }

    /// <summary>
    /// Add a log message on exit.
    /// </summary>
    public StateMachineBuilder OnExitLog(string message)
    {
        EnsureState();
        _currentState!.ExitActions.Add(new LogAction { Message = message });
        return this;
    }



    // ════════════════════════════════════════════════════════════
    // Transitions
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Add a transition from the current state.
    /// Pass null condition (or use When.Always) for unconditional transition.
    /// </summary>
    public StateMachineBuilder TransitionTo(string targetState, IAction? condition = null,
        int priority = 0, int timeoutMs = 0, int maxRetries = 0, bool isFallback = false)
    {
        EnsureState();
        var transition = new StateTransition
        {
            ToState = targetState,
            Condition = condition,
            Priority = priority,
            TimeoutMs = timeoutMs,
            MaxRetries = maxRetries,
            IsFallback = isFallback,
            TransitionType = condition == null ? TransitionType.Immediate : TransitionType.Polling
        };
        _currentState!.Transitions.Add(transition);
        return this;
    }

    /// <summary>
    /// Add a transition with on-transition actions.
    /// </summary>
    public StateMachineBuilder TransitionTo(string targetState, IAction? condition,
        int priority, params IAction[] onTransitionActions)
    {
        EnsureState();
        var transition = new StateTransition
        {
            ToState = targetState,
            Condition = condition,
            Priority = priority,
            TransitionType = condition == null ? TransitionType.Immediate : TransitionType.Polling
        };
        transition.OnTransitionActions.AddRange(onTransitionActions);
        _currentState!.Transitions.Add(transition);
        return this;
    }

    /// <summary>
    /// Shorthand: Add a fallback transition (checked last, no condition).
    /// </summary>
    public StateMachineBuilder Fallback(string targetState, int timeoutMs = 0)
    {
        EnsureState();
        _currentState!.Transitions.Add(new StateTransition
        {
            ToState = targetState,
            IsFallback = true,
            TimeoutMs = timeoutMs,
            TransitionType = timeoutMs > 0 ? TransitionType.Polling : TransitionType.Immediate
        });
        return this;
    }

    // ════════════════════════════════════════════════════════════
    // Global Transitions (Interrupts)
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Add a global transition (checked in ALL states, highest priority interrupts).
    /// </summary>
    public StateMachineBuilder GlobalTransition(string targetState, IAction condition,
        int priority = 100, params IAction[] onTransitionActions)
    {
        var gt = new StateTransition
        {
            ToState = targetState,
            Condition = condition,
            Priority = priority,
            TransitionType = TransitionType.Polling
        };
        gt.OnTransitionActions.AddRange(onTransitionActions);
        _globalTransitions.Add(gt);
        return this;
    }

    // ════════════════════════════════════════════════════════════
    // Build
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Finalize and return the constructed StateMachine.
    /// Runs validation and throws if there are configuration errors.
    /// </summary>
    public StateMachine Build()
    {
        FinalizeCurrentState();

        // Add global transitions
        foreach (var gt in _globalTransitions)
        {
            _machine.GlobalTransitions.Add(gt);
        }

        // Auto-set initial state if not specified
        if (string.IsNullOrEmpty(_machine.InitialStateName) && _machine.States.Count > 0)
        {
            _machine.InitialStateName = _machine.States.First().Name;
        }

        // Run validation
        var validation = StateMachineValidator.Validate(_machine);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"StateMachine validation failed:\n{string.Join("\n", validation.Errors)}");
        }

        // Log warnings
        foreach (var warning in validation.Warnings)
        {
            System.Diagnostics.Debug.WriteLine($"[StateMachineBuilder] Warning: {warning}");
        }

        return _machine;
    }

    // ════════════════════════════════════════════════════════════
    // Internal Helpers
    // ════════════════════════════════════════════════════════════

    private void FinalizeCurrentState()
    {
        if (_currentState != null)
        {
            _machine.States.Add(_currentState);
            _currentState = null;
        }
    }

    private void EnsureState()
    {
        if (_currentState == null)
        {
            throw new InvalidOperationException(
                "No state defined. Call .State(\"name\") before adding actions or transitions.");
        }
    }
}
