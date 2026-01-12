using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Base class for actions providing common property implementations.
/// </summary>
public abstract class ActionBase : IAction, System.ComponentModel.INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    // Abstract so derived classes must implement it or override it
    public abstract string DisplayName { get; }
    
    public bool IsBreakpoint { get; set; }
    
    // New properties from IAction
    public int RetryCount { get; set; } = 0;
    public int RetryIntervalMs { get; set; } = 1000;
    public bool ContinueOnError { get; set; } = false;

    public abstract Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct);

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
