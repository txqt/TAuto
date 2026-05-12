using System;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.Services;

/// <summary>
/// Implements IUserInterfaceAdapter using System.Console.
/// </summary>
public class ConsoleInteractionService : IUserInterfaceAdapter
{
    public bool IsInteractive
    {
        get
        {
            try { return Console.WindowHeight > 0; }
            catch { return false; }
        }
    }

    public void WriteMessage(string message)
    {
        Console.WriteLine(message);
    }

    public async Task<int> ShowMenuAsync(string title, params string[] options)
    {
        if (options == null || options.Length == 0) return -1;
        if (!IsInteractive) return 0;

        return await Task.Run(() =>
        {
            int selectedIndex = 0;
            bool done = false;

            // Hide cursor
            try { Console.CursorVisible = false; } catch { }

            while (!done)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"=== {title} ===");
                Console.ResetColor();
                Console.WriteLine("Use ↑/↓ to navigate, Enter to select.\n");

                for (int i = 0; i < options.Length; i++)
                {
                    if (i == selectedIndex)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($" > {options[i]}");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"   {options[i]}");
                    }
                }

                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        selectedIndex--;
                        if (selectedIndex < 0) selectedIndex = options.Length - 1;
                        break;
                    case ConsoleKey.DownArrow:
                        selectedIndex++;
                        if (selectedIndex >= options.Length) selectedIndex = 0;
                        break;
                    case ConsoleKey.Enter:
                        done = true;
                        break;
                }
            }

            try { Console.CursorVisible = true; } catch { }
            Console.Clear(); // Clear menu after selection
            
            return selectedIndex;
        });
    }
}
