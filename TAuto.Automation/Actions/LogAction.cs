using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Automation.Models;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Logs a message to the script console.
/// </summary>
[ActionMetadata("Log", "System", "📢")]
public class LogAction : ActionBase
{
    public override string DisplayName => $"Log: {Message}";

    [ActionParameter("Message", "Message to write to the execution log.")]
    public string Message { get; set; } = string.Empty;

    public override Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        var processedMessage = ReplaceVariables(Message, context);
        context.Logger?.Info(processedMessage);
        return Task.FromResult(ActionResult.Ok(processedMessage));
    }

    private static string ReplaceVariables(string input, ScriptContext context)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return Regex.Replace(input, @"\{(.+?)\}", match =>
        {
            var variableName = match.Groups[1].Value;
            return context.HasVariable(variableName) ? context.GetString(variableName) : match.Value;
        });
    }
}
