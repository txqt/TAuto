namespace TAuto.Automation.StateMachine.Components;

public interface IVariableStore
{
    void ClearLocalVariables(string stateName);
    void SetVariable(string name, object value);
    T? GetVariable<T>(string name, T? defaultValue = default);
}
