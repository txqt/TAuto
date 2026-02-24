using System.Drawing;

namespace TAuto.Core;

/// <summary>
/// Result of an action execution.
/// </summary>
public class ActionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
    
    public static ActionResult Ok(object? data = null)
        => new() { Success = true, Data = data };
    
    public static ActionResult Ok(Point location)
        => new() { Success = true, Data = location };
    
    public static ActionResult Fail(string message)
        => new() { Success = false, Message = message };
    
    public static ActionResult Jump(string targetActionId)
        => new() { Success = true, Data = targetActionId };

    public static ActionResult Log(string message)
        => new() { Success = true, Message = message };
}
