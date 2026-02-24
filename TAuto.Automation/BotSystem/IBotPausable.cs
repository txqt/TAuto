using System;
using System.Threading.Tasks;

namespace TAuto.Automation.BotSystem;

public interface IBotPausable
{
    bool IsPaused { get; }
    event Action<bool>? OnPausedStateChanged;
    
    void Pause();
    void Resume();
    Task CheckPausedAsync();
}
