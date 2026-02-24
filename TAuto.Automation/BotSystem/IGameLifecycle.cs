using System.Threading.Tasks;

namespace TAuto.Automation.BotSystem;

public interface IGameLifecycle
{
    string? GamePackageName { get; set; }
    Task<bool> RestartGameAsync(int loadWaitMs = 15000);
    Task<bool> ForceStopGameAsync();
}
