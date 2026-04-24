using System.Text.Json.Serialization;
using TAuto.Core;
using TAuto.Core.Models;
using TAuto.Automation.Actions;

namespace TAuto.Automation.Models;

/// <summary>
/// Source-generated JSON context for AutoBot models and actions.
/// Required for Native AOT support in .NET 8.0+.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = [typeof(ActionJsonConverter)])]
[JsonSerializable(typeof(BotProfile))]
[JsonSerializable(typeof(SessionState))]
[JsonSerializable(typeof(List<IAction>))]
[JsonSerializable(typeof(TapAction))]
[JsonSerializable(typeof(ClickImageAction))]
[JsonSerializable(typeof(ClickTextAction))]
[JsonSerializable(typeof(DelayAction))]
[JsonSerializable(typeof(DelegateAction))]
[JsonSerializable(typeof(ExtractTextAction))]
[JsonSerializable(typeof(FindColorAction))]
[JsonSerializable(typeof(FindImageAction))]
[JsonSerializable(typeof(GetClipboardAction))]
[JsonSerializable(typeof(IfImageFoundAction))]
[JsonSerializable(typeof(IfTextFoundAction))]
[JsonSerializable(typeof(IfVariableAction))]
[JsonSerializable(typeof(LogAction))]
[JsonSerializable(typeof(LongPressAction))]
[JsonSerializable(typeof(PressKeyAction))]
[JsonSerializable(typeof(ReliableClickAction))]
[JsonSerializable(typeof(RestartGameAction))]
[JsonSerializable(typeof(SetRandomVariableAction))]
[JsonSerializable(typeof(SetVariableAction))]
[JsonSerializable(typeof(SwipeAction))]
[JsonSerializable(typeof(WaitForColorAction))]
[JsonSerializable(typeof(WaitForImageAction))]
[JsonSerializable(typeof(List<ScheduledJob>))]
internal partial class AutomationJsonContext : JsonSerializerContext
{
}
