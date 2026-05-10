using TAuto.Automation.Actions;
using TAuto.Core;

namespace TAuto.Automation.Models;

/// <summary>
/// AOT-compatible action factory that maps type identifiers to concrete action instances
/// without using reflection. This enables Native AOT compilation for the SaaS Worker.
/// 
/// When adding a new action, register it once in the table below.
/// </summary>
public static class StaticActionFactory
{
    private sealed record ActionRegistration(string Identifier, Type Type, Func<IAction> Factory);

    /// <summary>
    /// All known action type mappings.
    /// The identifier is the FullName of the action class (e.g. "TAuto.Automation.Actions.TapAction").
    /// </summary>
    private static readonly ActionRegistration[] _registrations =
    [
        Register<ClickImageAction>("TAuto.Automation.Actions.ClickImageAction", () => new ClickImageAction()),
        Register<ClickTextAction>("TAuto.Automation.Actions.ClickTextAction", () => new ClickTextAction()),
        Register<DelayAction>("TAuto.Automation.Actions.DelayAction", () => new DelayAction()),
        Register<ExtractTextAction>("TAuto.Automation.Actions.ExtractTextAction", () => new ExtractTextAction()),
        Register<FindColorAction>("TAuto.Automation.Actions.FindColorAction", () => new FindColorAction()),
        Register<FindImageAction>("TAuto.Automation.Actions.FindImageAction", () => new FindImageAction()),
        Register<GetClipboardAction>("TAuto.Automation.Actions.GetClipboardAction", () => new GetClipboardAction()),
        Register<IfImageFoundAction>("TAuto.Automation.Actions.IfImageFoundAction", () => new IfImageFoundAction()),
        Register<IfTextFoundAction>("TAuto.Automation.Actions.IfTextFoundAction", () => new IfTextFoundAction()),
        Register<IfVariableAction>("TAuto.Automation.Actions.IfVariableAction", () => new IfVariableAction()),
        Register<LogAction>("TAuto.Automation.Actions.LogAction", () => new LogAction()),
        Register<LongPressAction>("TAuto.Automation.Actions.LongPressAction", () => new LongPressAction()),
        Register<PressKeyAction>("TAuto.Automation.Actions.PressKeyAction", () => new PressKeyAction()),
        Register<ReliableClickAction>("TAuto.Automation.Actions.ReliableClickAction", () => new ReliableClickAction()),
        Register<RestartGameAction>("TAuto.Automation.Actions.RestartGameAction", () => new RestartGameAction()),
        Register<SetRandomVariableAction>("TAuto.Automation.Actions.SetRandomVariableAction", () => new SetRandomVariableAction()),
        Register<SetVariableAction>("TAuto.Automation.Actions.SetVariableAction", () => new SetVariableAction()),
        Register<SwipeAction>("TAuto.Automation.Actions.SwipeAction", () => new SwipeAction()),
        Register<TapAction>("TAuto.Automation.Actions.TapAction", () => new TapAction()),
        Register<WaitForColorAction>("TAuto.Automation.Actions.WaitForColorAction", () => new WaitForColorAction()),
        Register<WaitForImageAction>("TAuto.Automation.Actions.WaitForImageAction", () => new WaitForImageAction()),
    ];

    private static readonly Dictionary<string, Func<IAction>> _factories = _registrations
        .ToDictionary(r => r.Identifier, r => r.Factory, StringComparer.Ordinal);

    private static readonly Dictionary<string, Type> _types = _registrations
        .ToDictionary(r => r.Identifier, r => r.Type, StringComparer.Ordinal);

    /// <summary>
    /// Reverse mapping: Type → identifier string (for serialization).
    /// </summary>
    private static readonly Dictionary<Type, string> _reverseMap = _registrations
        .ToDictionary(r => r.Type, r => r.Identifier);

    private static ActionRegistration Register<TAction>(string identifier, Func<IAction> factory)
        where TAction : IAction
    {
        return new ActionRegistration(identifier, typeof(TAction), factory);
    }

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
        return _types.TryGetValue(typeIdentifier, out var type) ? type : null;
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
