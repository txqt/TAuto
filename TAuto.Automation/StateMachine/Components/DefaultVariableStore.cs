using System;
using TAuto.Core;

namespace TAuto.Automation.StateMachine.Components;

public class DefaultVariableStore : IVariableStore
{
    private readonly ScriptContext _context;

    public DefaultVariableStore(ScriptContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void ClearLocalVariables(string stateName)
    {
        _context.ClearLocalVariables(stateName);
    }

    public void SetVariable(string name, object value)
    {
        _context.SetVariable(name, value);
    }

    public T? GetVariable<T>(string name, T? defaultValue = default)
    {
        return _context.GetVariable<T>(name, defaultValue!);
    }
}
