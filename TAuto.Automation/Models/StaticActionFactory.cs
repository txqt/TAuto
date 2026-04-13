using TAuto.Automation.Actions;
using TAuto.Core;

namespace TAuto.Automation.Models;

/// <summary>
/// AOT-compatible action factory that maps type identifiers to concrete action instances
/// without using reflection. This enables Native AOT compilation for the SaaS Worker.
/// 
/// When adding a new action, you MUST register it here in both dictionaries.
/// </summary>
public static class StaticActionFactory
{
    /// <summary>
    /// All known action type mappings: identifier → constructor.
    /// The identifier is the FullName of the action class (e.g. "TAuto.Automation.Actions.TapAction").
    /// </summary>
    private static readonly Dictionary<string, Func<IAction>> _factories = new(StringComparer.Ordinal)
    {
        ["TAuto.Automation.Actions.ClickImageAction"] = () => new ClickImageAction(),
        ["TAuto.Automation.Actions.ClickTextAction"] = () => new ClickTextAction(),
        ["TAuto.Automation.Actions.DelayAction"] = () => new DelayAction(),
        ["TAuto.Automation.Actions.ExtractTextAction"] = () => new ExtractTextAction(),
        ["TAuto.Automation.Actions.FindColorAction"] = () => new FindColorAction(),
        ["TAuto.Automation.Actions.FindImageAction"] = () => new FindImageAction(),
        ["TAuto.Automation.Actions.GetClipboardAction"] = () => new GetClipboardAction(),
        ["TAuto.Automation.Actions.IfImageFoundAction"] = () => new IfImageFoundAction(),
        ["TAuto.Automation.Actions.IfTextFoundAction"] = () => new IfTextFoundAction(),
        ["TAuto.Automation.Actions.IfVariableAction"] = () => new IfVariableAction(),
        ["TAuto.Automation.Actions.LogAction"] = () => new LogAction(),
        ["TAuto.Automation.Actions.LongPressAction"] = () => new LongPressAction(),
        ["TAuto.Automation.Actions.PressKeyAction"] = () => new PressKeyAction(),
        ["TAuto.Automation.Actions.ReliableClickAction"] = () => new ReliableClickAction(),
        ["TAuto.Automation.Actions.RestartGameAction"] = () => new RestartGameAction(),
        ["TAuto.Automation.Actions.SetRandomVariableAction"] = () => new SetRandomVariableAction(),
        ["TAuto.Automation.Actions.SetVariableAction"] = () => new SetVariableAction(),
        ["TAuto.Automation.Actions.SwipeAction"] = () => new SwipeAction(),
        ["TAuto.Automation.Actions.TapAction"] = () => new TapAction(),
        ["TAuto.Automation.Actions.WaitForColorAction"] = () => new WaitForColorAction(),
        ["TAuto.Automation.Actions.WaitForImageAction"] = () => new WaitForImageAction(),
    };

    /// <summary>
    /// Reverse mapping: Type → identifier string (for serialization).
    /// </summary>
    private static readonly Dictionary<Type, string> _reverseMap = new()
    {
        [typeof(ClickImageAction)] = "TAuto.Automation.Actions.ClickImageAction",
        [typeof(ClickTextAction)] = "TAuto.Automation.Actions.ClickTextAction",
        [typeof(DelayAction)] = "TAuto.Automation.Actions.DelayAction",
        [typeof(ExtractTextAction)] = "TAuto.Automation.Actions.ExtractTextAction",
        [typeof(FindColorAction)] = "TAuto.Automation.Actions.FindColorAction",
        [typeof(FindImageAction)] = "TAuto.Automation.Actions.FindImageAction",
        [typeof(GetClipboardAction)] = "TAuto.Automation.Actions.GetClipboardAction",
        [typeof(IfImageFoundAction)] = "TAuto.Automation.Actions.IfImageFoundAction",
        [typeof(IfTextFoundAction)] = "TAuto.Automation.Actions.IfTextFoundAction",
        [typeof(IfVariableAction)] = "TAuto.Automation.Actions.IfVariableAction",
        [typeof(LogAction)] = "TAuto.Automation.Actions.LogAction",
        [typeof(LongPressAction)] = "TAuto.Automation.Actions.LongPressAction",
        [typeof(PressKeyAction)] = "TAuto.Automation.Actions.PressKeyAction",
        [typeof(ReliableClickAction)] = "TAuto.Automation.Actions.ReliableClickAction",
        [typeof(RestartGameAction)] = "TAuto.Automation.Actions.RestartGameAction",
        [typeof(SetRandomVariableAction)] = "TAuto.Automation.Actions.SetRandomVariableAction",
        [typeof(SetVariableAction)] = "TAuto.Automation.Actions.SetVariableAction",
        [typeof(SwipeAction)] = "TAuto.Automation.Actions.SwipeAction",
        [typeof(TapAction)] = "TAuto.Automation.Actions.TapAction",
        [typeof(WaitForColorAction)] = "TAuto.Automation.Actions.WaitForColorAction",
        [typeof(WaitForImageAction)] = "TAuto.Automation.Actions.WaitForImageAction",
    };

    /// <summary>
    /// Create an action instance from its type identifier string.
    /// Returns null if the identifier is unknown.
    /// </summary>
    public static IAction? CreateAction(string typeIdentifier)
    {
        return _factories.TryGetValue(typeIdentifier, out var factory) ? factory() : null;
    }

    /// <summary>
    /// Get the type identifier string for a given action type.
    /// Returns null if the type is not registered.
    /// </summary>
    public static string? GetIdentifier(Type actionType)
    {
        return _reverseMap.TryGetValue(actionType, out var id) ? id : null;
    }

    /// <summary>
    /// Try to resolve the CLR Type for a given identifier.
    /// </summary>
    public static Type? GetActionType(string typeIdentifier)
    {
        if (_factories.TryGetValue(typeIdentifier, out var factory))
        {
            // Create a temporary instance to get the type, then discard it.
            // This is only called during converter initialization, not per-action.
            return factory().GetType();
        }
        return null;
    }

    /// <summary>
    /// Get all registered identifier → Type pairs (for converter initialization).
    /// </summary>
    public static IReadOnlyDictionary<string, Func<IAction>> GetAllFactories() => _factories;

    /// <summary>
    /// Get the reverse map for serialization.
    /// </summary>
    public static IReadOnlyDictionary<Type, string> GetReverseMap() => _reverseMap;
}
