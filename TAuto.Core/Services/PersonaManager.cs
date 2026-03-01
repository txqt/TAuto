using System;
using System.IO;
using System.Text.Json;
using TAuto.Core.Models;

namespace TAuto.Core.Services;

/// <summary>
/// Manages BotPersona and BotSession persistence.
/// Storage: %APPDATA%\AutoBot\Personas\ and %APPDATA%\AutoBot\Sessions\.
/// </summary>
public class PersonaManager
{
    private static readonly string AppDataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoBot");

    private static readonly string PersonaDir = Path.Combine(AppDataRoot, "Personas");
    private static readonly string SessionDir = Path.Combine(AppDataRoot, "Sessions");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Persona ──

    /// <summary>
    /// Load persona for a bot ID. Creates a new random persona if none exists.
    /// Applies daily drift automatically.
    /// </summary>
    public BotPersona LoadOrCreatePersona(string botId)
    {
        var path = Path.Combine(PersonaDir, $"{SanitizeId(botId)}.json");
        BotPersona persona;

        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                persona = JsonSerializer.Deserialize<BotPersona>(json, JsonOpts)
                    ?? BotPersona.GenerateRandom(botId);
            }
            catch
            {
                persona = BotPersona.GenerateRandom(botId);
            }
        }
        else
        {
            persona = BotPersona.GenerateRandom(botId);
        }

        persona.ApplyDailyDrift();
        SavePersona(persona);
        return persona;
    }

    public void SavePersona(BotPersona persona)
    {
        try
        {
            Directory.CreateDirectory(PersonaDir);
            var path = Path.Combine(PersonaDir, $"{SanitizeId(persona.Id)}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(persona, JsonOpts));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PersonaManager] Save persona failed: {ex.Message}");
        }
    }

    // ── Session ──

    public BotSession LoadOrCreateSession(string botId)
    {
        var path = Path.Combine(SessionDir, $"{SanitizeId(botId)}.json");

        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<BotSession>(json, JsonOpts)
                    ?? new BotSession { BotId = botId };
            }
            catch { }
        }
        return new BotSession { BotId = botId };
    }

    public void SaveSession(BotSession session)
    {
        try
        {
            Directory.CreateDirectory(SessionDir);
            var path = Path.Combine(SessionDir, $"{SanitizeId(session.BotId)}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(session, JsonOpts));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PersonaManager] Save session failed: {ex.Message}");
        }
    }

    private static string SanitizeId(string id)
        => string.Join("_", id.Split(Path.GetInvalidFileNameChars()));
}
