using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Logs a message to the script console.
/// </summary>
public class LogAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => $"ðŸ“ Log: {Message}";
    
    public string Message { get; set; } = "";

    public override Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        // Interpolate variables: "Hello {Name}" -> "Hello World"
        string processedMessage = ReplaceVariables(Message, context);
        
        return Task.FromResult(ActionResult.Ok(processedMessage));
    }
    
    private string ReplaceVariables(string input, ScriptContext context)
    {
        if (string.IsNullOrEmpty(input)) return "";
        
        return Regex.Replace(input, @"\{(.+?)\}", match =>
        {
            string varName = match.Groups[1].Value;
            if (context.HasVariable(varName))
            {
                return context.GetString(varName);
            }
            return match.Value; // Keep original if not found
        });
    }
}
