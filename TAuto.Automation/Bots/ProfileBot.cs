using System;
using System.IO;
using System.Threading.Tasks;
using TAuto.Automation.Models;
using TAuto.Automation.StateMachine;
using Microsoft.Extensions.Logging;

namespace TAuto.Automation.Bots;

/// <summary>
/// A generic bot that loads a script.json profile and executes its state machine.
/// This enables the "Code-Free" bot experience in AutoBot Dashboard.
/// </summary>
public class ProfileBot : BotBase
{
    public override async Task RunAsync()
    {
        // 1. Resolve script path
        string? baseDir = Context.GetVariable<string>("BaseDirectory");
        string scriptPath;
        
        if (!string.IsNullOrEmpty(baseDir))
        {
            scriptPath = Path.Combine(baseDir, "script.json");
        }
        else
        {
            scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "script.json");
        }
        
        if (!File.Exists(scriptPath))
        {
            // Fallback for different execution contexts
            scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "script.json");
        }

        if (!File.Exists(scriptPath))
        {
            Log($"❌ Error: Script file not found. Expected at: {scriptPath}");
            return;
        }

        // 2. Load and Deserialize
        Log($"📄 Loading bot profile from: {Path.GetFileName(scriptPath)}");
        
        try
        {
            var json = await File.ReadAllTextAsync(scriptPath);
            var profile = BotProfileSerializer.Deserialize(json);

            if (profile == null)
            {
                Log("❌ Error: Failed to parse bot profile (script.json is malformed).");
                return;
            }

            Log($"🚀 Starting Bot: {profile.Name} (v{profile.Version})");
            Log($"📝 Description: {profile.Description}");

            // 3. Execution
            // Ensure local resource paths are resolved correctly by the actions
            // (The exporter should have handled path hashing already)
            
            var result = await profile.StateMachine.RunAsync(Context, CancellationToken);

            if (result.Success)
            {
                Log($"✅ Bot execution completed: {result.Message}");
            }
            else
            {
                Log($"⚠️ Bot execution failed: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            Log($"💥 Fatal Error during script execution: {ex.Message}");
            Logger?.LogError(ex, "ProfileBot execution failed.");
        }
    }
}
