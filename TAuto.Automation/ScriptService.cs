using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace TAuto.Automation;

/// <summary>
/// Service for saving and loading automation scripts (new format with IAction).
/// Supports polymorphic action serialization.
/// </summary>
public class ScriptService
{
    private readonly string _scriptsDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public ScriptService()
    {
        // Store scripts in AppData/ADBCaptureV2/Scripts
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _scriptsDirectory = Path.Combine(appData, "ADBCaptureV2", "Scripts");
        
        // Ensure directory exists
        if (!Directory.Exists(_scriptsDirectory))
        {
            Directory.CreateDirectory(_scriptsDirectory);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new ActionJsonConverter() }
        };
    }

    /// <summary>
    /// Get the path for a script file.
    /// </summary>
    private string GetScriptPath(string scriptName)
    {
        string safeName = string.Join("_", scriptName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_scriptsDirectory, $"{safeName}.json");
    }

    /// <summary>
    /// Save a script to disk.
    /// </summary>
    public async Task SaveScriptAsync(AutomationScript script)
    {
        if (string.IsNullOrWhiteSpace(script.Name))
        {
            throw new ArgumentException("Script name cannot be empty");
        }

        script.ModifiedAt = DateTime.Now;
        
        string filePath = GetScriptPath(script.Name);
        string json = JsonSerializer.Serialize(script, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Load a script from disk.
    /// </summary>
    public async Task<AutomationScript?> LoadScriptAsync(string scriptName)
    {
        string filePath = GetScriptPath(scriptName);
        
        if (!File.Exists(filePath))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<AutomationScript>(json, _jsonOptions);
    }

    /// <summary>
    /// Load a script from a specific file path.
    /// </summary>
    public async Task<AutomationScript?> LoadScriptFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<AutomationScript>(json, _jsonOptions);
    }

    /// <summary>
    /// Save a script to a specific file path.
    /// </summary>
    public async Task SaveScriptToFileAsync(AutomationScript script, string filePath)
    {
        script.ModifiedAt = DateTime.Now;
        string json = JsonSerializer.Serialize(script, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Get all saved scripts.
    /// </summary>
    public async Task<List<AutomationScript>> GetScriptsAsync()
    {
        var scripts = new List<AutomationScript>();
        
        if (!Directory.Exists(_scriptsDirectory))
        {
            return scripts;
        }

        var files = Directory.GetFiles(_scriptsDirectory, "*.json");
        
        foreach (var file in files)
        {
            try
            {
                string json = await File.ReadAllTextAsync(file);
                var script = JsonSerializer.Deserialize<AutomationScript>(json, _jsonOptions);
                if (script != null)
                {
                    scripts.Add(script);
                }
            }
            catch
            {
                // Skip invalid files
            }
        }

        return scripts.OrderByDescending(s => s.ModifiedAt).ToList();
    }

    /// <summary>
    /// Get just the script names (faster than loading all scripts).
    /// </summary>
    public List<string> GetScriptNames()
    {
        if (!Directory.Exists(_scriptsDirectory))
        {
            return new List<string>();
        }

        return Directory.GetFiles(_scriptsDirectory, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n)
            .ToList();
    }

    /// <summary>
    /// Delete a script.
    /// </summary>
    public bool DeleteScript(string scriptName)
    {
        string filePath = GetScriptPath(scriptName);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Check if a script exists.
    /// </summary>
    public bool ScriptExists(string scriptName)
    {
        return File.Exists(GetScriptPath(scriptName));
    }

    /// <summary>
    /// Get the scripts directory path.
    /// </summary>
    public string GetScriptsDirectory()
    {
        return _scriptsDirectory;
    }

    /// <summary>
    /// Export a script to a shareable JSON string.
    /// </summary>
    public string ExportToJson(AutomationScript script)
    {
        return JsonSerializer.Serialize(script, _jsonOptions);
    }

    /// <summary>
    /// Import a script from JSON string.
    /// </summary>
    public AutomationScript? ImportFromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AutomationScript>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
