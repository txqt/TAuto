using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Retrieves text from the system clipboard.
/// </summary>
public class GetClipboardAction : ActionBase
{
    public override string DisplayName => $"📋 Get Clipboard to ${OutputVariable}";

    /// <summary>
    /// Variable name to store the clipboard text.
    /// </summary>
    public string OutputVariable { get; set; } = "ClipboardText";

    /// <summary>
    /// Optional: Log the text automatically.
    /// </summary>
    public bool LogResult { get; set; } = true;

    public override Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        string text = string.Empty;
        Exception? error = null;

        // Clipboard access must be on STA thread (UI thread)
        Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    text = Clipboard.GetText();
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        if (error != null)
            return Task.FromResult(ActionResult.Fail($"Clipboard Error: {error.Message}"));

        context.SetVariable(OutputVariable, text);

        if (LogResult)
            context.Logger?.Info($"Clipboard [{OutputVariable}]: {text}");

        return Task.FromResult(ActionResult.Ok());
    }
}
