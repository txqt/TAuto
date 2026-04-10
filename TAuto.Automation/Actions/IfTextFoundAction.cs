using TAuto.Automation.Models;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Conditional action that checks if text exists on screen using OCR.
/// </summary>
[ActionMetadata("If Text Found", "Vision & OCR", "❓", IsAdvanced = true)]
public class IfTextFoundAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => $"❓ If Text Found '{TargetText}'";
    
    // ===== Configuration =====
    
    [ActionParameter("Name", "Friendly name for this action.")]
    public string Name { get; set; } = string.Empty;
    
    [ActionParameter("Target Text", "The text to search for on screen.")]
    public string TargetText { get; set; } = string.Empty;
    
    [ActionParameter("Case Sensitive", "Match upper/lower case strictly.", IsAdvanced = true)]
    public bool CaseSensitive { get; set; } = false;
    
    [ActionParameter("Partial Match", "Allow matching of sub-strings.", IsAdvanced = true)]
    public bool PartialMatch { get; set; } = true;
    
    [ActionParameter("Then Action", "Action to jump to if text is found.", EditorType = ActionParameterEditorType.ActionId)]
    public string ThenActionId { get; set; } = string.Empty;

    [ActionParameter("Else Action", "Action to jump to if text is NOT found.", EditorType = ActionParameterEditorType.ActionId)]
    public string ElseActionId { get; set; } = string.Empty;
    
    [ActionParameter("Fresh Capture", "Force a new screen capture before checking.", IsAdvanced = true)]
    public bool ForceFreshCapture { get; set; } = true;
    
    private int _consecutiveFrames = 1;
    [ActionParameter("Consecutive Frames", "Wait for the text to appear for N frames.", IsAdvanced = true)]
    public int ConsecutiveFrames 
    { 
        get => _consecutiveFrames; 
        set => SetProperty(ref _consecutiveFrames, value); 
    }
    
    // ===== Execute =====
    
    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return ActionResult.Fail("Cancelled");
        
        if (string.IsNullOrEmpty(TargetText))
            return ActionResult.Fail("Target text not set");
        
        var confirmation = new TAuto.Automation.Utilities.DetectionConfirmation(ConsecutiveFrames);
        bool isConfirmed = false;
        
        int maxAttempts = ConsecutiveFrames == 1 ? 1 : ConsecutiveFrames + 5;
        for (int i = 0; i < maxAttempts; i++)
        {
            await context.UpdateScreenCaptureAsync(force: ForceFreshCapture);
            if (context.LastScreenCapture == null)
                return ActionResult.Fail("Cannot capture screen");
            
            string foundText = context.Ocr.GetText(context.LastScreenCapture);
            bool found = !string.IsNullOrEmpty(foundText) && CheckMatch(foundText);
            
            if (confirmation.RecordResult(found))
            {
                isConfirmed = true;
                break;
            }
            
            if (ConsecutiveFrames > 1 && !isConfirmed && i < maxAttempts - 1)
            {
                await Task.Delay(200, ct);
            }
        }

        return HandleBranch(isConfirmed, context);
    }
    
    private bool CheckMatch(string source)
    {
        string target = TargetText;
        StringComparison comparison = CaseSensitive 
            ? StringComparison.Ordinal 
            : StringComparison.OrdinalIgnoreCase;
            
        if (PartialMatch)
        {
            return source.IndexOf(target, comparison) >= 0;
        }
        else
        {
            return source.Equals(target, comparison);
        }
    }
    
    private ActionResult HandleBranch(bool found, ScriptContext context)
    {
        if (found)
        {
            if (!string.IsNullOrEmpty(ThenActionId))
                return ActionResult.Jump(ThenActionId);
        }
        else
        {
            if (!string.IsNullOrEmpty(ElseActionId))
                return ActionResult.Jump(ElseActionId);
        }
        
        return ActionResult.Ok();
    }
}
