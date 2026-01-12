using System;

namespace TAuto.Core.Models;

public class ErrorPolicy
{
    public int GlobalMaxRetries { get; set; } = 10;
    public bool ScreenshotOnError { get; set; } = true;
    public string ScreenshotDirectory { get; set; } = "Screenshots/Errors";
    
    /// <summary>
    /// If true, catastrophic errors (non-recoverable) will stop the script even if ContinueOnError is true on the action.
    /// </summary>
    public bool FailFastOnCriticalError { get; set; } = true;
}
