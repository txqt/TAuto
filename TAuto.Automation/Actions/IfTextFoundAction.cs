using TAuto.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TAuto.Automation.Actions;

/// <summary>
/// Conditional action that checks if text exists on screen using OCR.
/// </summary>
public class IfTextFoundAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => $"â“ If Text Found '{TargetText}'";
    
    // ===== Configuration =====
    
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Text to search for.
    /// </summary>
    public string TargetText { get; set; } = string.Empty;
    
    /// <summary>
    /// If true, matches case. If false, ignores case.
    /// </summary>
    public bool CaseSensitive { get; set; } = false;
    
    /// <summary>
    /// If true, looks for partial match (Contains). If false, exact match (Equals).
    /// </summary>
    public bool PartialMatch { get; set; } = true;
    
    public string ThenActionId { get; set; } = string.Empty;
    public string ElseActionId { get; set; } = string.Empty;
    
    public bool ForceFreshCapture { get; set; } = true;
    
    // ===== Execute =====
    
    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return ActionResult.Fail("Cancelled");
        
        if (string.IsNullOrEmpty(TargetText))
            return ActionResult.Fail("Target text not set");
        
        // Mobile screenshot capture
        await context.UpdateScreenCaptureAsync(force: ForceFreshCapture);
        if (context.LastScreenCapture == null)
            return ActionResult.Fail("Cannot capture screen");
        
        // Perform OCR
        string foundText = context.Ocr.GetText(context.LastScreenCapture);
        if (string.IsNullOrEmpty(foundText))
        {
            // No text found at all -> goes to ELSE
            return HandleBranch(false, context);
        }

        bool found = CheckMatch(foundText);
        return HandleBranch(found, context);
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
