using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TAuto.Core;

namespace TAuto.Automation.BotSystem;

public class DefaultGameLifecycle : IGameLifecycle
{
    private readonly Func<ScriptContext> _contextProvider;
    private readonly Func<ILogger?> _loggerProvider;

    public DefaultGameLifecycle(Func<ScriptContext> contextProvider, Func<ILogger?> loggerProvider)
    {
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        _loggerProvider = loggerProvider ?? throw new ArgumentNullException(nameof(loggerProvider));
    }

    public string? GamePackageName { get; set; }

    public async Task<bool> RestartGameAsync(int loadWaitMs = 15000)
    {
        if (string.IsNullOrEmpty(GamePackageName))
        {
            _loggerProvider()?.LogWarning("Cannot restart game: GamePackageName not set");
            return false;
        }

        _loggerProvider()?.LogInformation($"Restarting game: {GamePackageName}");

        var context = _contextProvider();

        // Step 1: Force-stop
        await context.Device.ForceStopAppAsync(GamePackageName);
        await Task.Delay(2000);

        // Step 2: Relaunch
        bool launched = await context.Device.LaunchAppAsync(GamePackageName);
        if (!launched)
        {
            _loggerProvider()?.LogWarning("Failed to launch game");
            return false;
        }

        // Step 3: Wait for game to load
        _loggerProvider()?.LogInformation($"Game launched, waiting {loadWaitMs}ms for load...");
        await Task.Delay(loadWaitMs);

        _loggerProvider()?.LogInformation("Game restart complete");
        return true;
    }

    public async Task<bool> ForceStopGameAsync()
    {
        if (string.IsNullOrEmpty(GamePackageName))
        {
            _loggerProvider()?.LogWarning("Cannot stop game: GamePackageName not set");
            return false;
        }
        var context = _contextProvider();
        return await context.Device.ForceStopAppAsync(GamePackageName);
    }
}
