using TAuto.Automation.StateMachine;

namespace TAuto.Automation.Models;

/// <summary>
/// Root model for the WinUI3 Bot Editor and automation runtime.
/// </summary>
public class BotProfile
{
    public const string CurrentSchemaVersion = "1.0.0";

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Bot";
    public string Version { get; set; } = CurrentSchemaVersion;
    public string Description { get; set; } = string.Empty;
    public BotPermissions Permissions { get; set; } = new();
    public StateMachine.StateMachine StateMachine { get; set; } = new();
}

public class BotPermissions
{
    public bool RequiresDiskAccess { get; set; }
    public bool RequiresNetworkAccess { get; set; }
    public bool RequiresProcessManagement { get; set; }
}
