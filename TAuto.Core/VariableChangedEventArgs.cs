using System;

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
