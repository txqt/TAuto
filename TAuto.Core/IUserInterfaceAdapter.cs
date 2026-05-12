using System.Threading.Tasks;

namespace TAuto.Core;

/// <summary>
/// Abstraction for bot-to-user interactions (UI, Console, etc.).
/// Decouples BotBase from specific UI implementations like System.Console.
/// </summary>
public interface IUserInterfaceAdapter
{
    /// <summary>
    /// Displays a menu and returns the selected index.
    /// </summary>
    Task<int> ShowMenuAsync(string title, params string[] options);

    /// <summary>
    /// Displays a message to the user.
    /// </summary>
    void WriteMessage(string message);
    
    /// <summary>
    /// Returns true if an interactive UI is available.
    /// </summary>
    bool IsInteractive { get; }
}

/// <summary>
/// Default implementation that does nothing or simple logging.
/// </summary>
public class NoOpUserInterfaceAdapter : IUserInterfaceAdapter
{
    public bool IsInteractive => false;
    public Task<int> ShowMenuAsync(string title, params string[] options) => Task.FromResult(0);
    public void WriteMessage(string message) { }
}
