using System;
using System.Collections.Generic;

namespace TAuto.Core.Models;

public class SessionState
{
    public Guid SessionId { get; set; }
    public string ScriptId { get; set; } = string.Empty;
    public string ScriptPath { get; set; } = string.Empty;
    public string DeviceSerial { get; set; } = string.Empty;
    public int CurrentIndex { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime LastSaveTime { get; set; }
    public bool IsCompleted { get; set; }
}
