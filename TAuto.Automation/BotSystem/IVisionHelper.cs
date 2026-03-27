using System.Drawing;
using System.Threading.Tasks;

namespace TAuto.Automation.BotSystem;

public interface IVisionHelper
{
    Task<Point?> FindImageAsync(string templatePath, double threshold = 0.8);
    Task<bool> ExistsAsync(string templatePath, double threshold = 0.8);
    Task<bool> ClickImageAsync(string templatePath, double threshold = 0.8, int offsetX = 0, int offsetY = 0);
    Task<bool> WaitForImageAsync(string templatePath, int timeoutMs = 5000, double threshold = 0.8);
    Task<Point?> FindColorAsync(Color color, int tolerance = 10, Rectangle? region = null, int minPixelCount = 1);
    Task<bool> WaitForColorAsync(Color color, int timeoutMs = 5000, int tolerance = 10, Rectangle? region = null, int minPixelCount = 1);
}
