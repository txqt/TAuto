namespace TAuto.Automation.StateMachine;

/// <summary>
/// Global trace sink for state machine execution.
/// Used by hosts to capture traces without modifying bot code.
/// </summary>
public static class StateMachineTraceRouter
{
    private static Action<StateMachineTraceEntry>? _sink;

    public static bool IsEnabled { get; private set; }

    public static void Enable(Action<StateMachineTraceEntry> sink)
    {
        _sink = sink;
        IsEnabled = true;
    }

    public static void Disable()
    {
        IsEnabled = false;
        _sink = null;
    }

    public static void Emit(StateMachineTraceEntry entry)
    {
        if (!IsEnabled || _sink == null) return;
        try { _sink(entry); } catch { /* best effort */ }
    }
}

