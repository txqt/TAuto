using System;
using System.Threading.Tasks;

namespace TAuto.Automation.Bots;

public class ExampleFarmingBot : BotBase
{
    public override async Task RunAsync()
    {
        Log("Starting Farming Bot...");

        while (!CancellationToken.IsCancellationRequested)
        {
            Log("Searching for enemy...");
            
            // 1. Find Enemy
            if (await WaitForImage("enemy_icon.png", timeoutMs: 2000))
            {
                Log("Enemy found! Attacking...");
                
                // 2. Attack
                if (await ClickImage("enemy_icon.png"))
                {
                    // Wait for attack animation
                    await Delay(1500); 
                    
                    // 3. Collect Loot if available
                    if (await Exists("loot_box.png"))
                    {
                        Log("Loot found! Collecting...");
                        await ClickImage("loot_box.png");
                        await Delay(500);
                        
                        // Close loot dialog
                        await TapPercent(90, 10); // Close button usually top-right
                    }
                }
            }
            else
            {
                Log("No enemy found. Moving to next area...");
                // Swipe to find next area
                await Swipe(800, 500, 200, 500); // Swipe Right to Left
                await Delay(1000);
            }
            
            // Wait before next loop
            await Delay(500);
        }
        
        Log("Bot stoped.");
    }
}
