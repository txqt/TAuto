using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace TAuto.Automation;

/// <summary>
/// Service for saving and loading game profiles.
/// </summary>
public class ProfileService
{
    private readonly string _profilesDirectory;
    private readonly JsonSerializerOptions _jsonOptions;

    public ProfileService()
    {
        // Store profiles in AppData/ADBCaptureV2/Profiles
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _profilesDirectory = Path.Combine(appData, "ADBCaptureV2", "Profiles");
        
        // Ensure directory exists
        if (!Directory.Exists(_profilesDirectory))
        {
            Directory.CreateDirectory(_profilesDirectory);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Get the path for a profile file
    /// </summary>
    private string GetProfilePath(string profileName)
    {
        // Sanitize filename
        string safeName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_profilesDirectory, $"{safeName}.json");
    }

    /// <summary>
    /// Save a profile to disk
    /// </summary>
    public async Task SaveProfileAsync(GameProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.ProfileName))
        {
            throw new ArgumentException("Profile name cannot be empty");
        }

        profile.ModifiedAt = DateTime.Now;
        
        string filePath = GetProfilePath(profile.ProfileName);
        string json = JsonSerializer.Serialize(profile, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Load a profile from disk
    /// </summary>
    public async Task<GameProfile?> LoadProfileAsync(string profileName)
    {
        string filePath = GetProfilePath(profileName);
        
        if (!File.Exists(filePath))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<GameProfile>(json, _jsonOptions);
    }

    /// <summary>
    /// Get all saved profiles
    /// </summary>
    public async Task<List<GameProfile>> GetProfilesAsync()
    {
        var profiles = new List<GameProfile>();
        
        if (!Directory.Exists(_profilesDirectory))
        {
            return profiles;
        }

        var files = Directory.GetFiles(_profilesDirectory, "*.json");
        
        foreach (var file in files)
        {
            try
            {
                string json = await File.ReadAllTextAsync(file);
                var profile = JsonSerializer.Deserialize<GameProfile>(json, _jsonOptions);
                if (profile != null)
                {
                    profiles.Add(profile);
                }
            }
            catch
            {
                // Skip invalid files
            }
        }

        return profiles.OrderByDescending(p => p.ModifiedAt).ToList();
    }

    /// <summary>
    /// Get just the profile names (faster than loading all profiles)
    /// </summary>
    public List<string> GetProfileNames()
    {
        if (!Directory.Exists(_profilesDirectory))
        {
            return new List<string>();
        }

        return Directory.GetFiles(_profilesDirectory, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n)
            .ToList();
    }

    /// <summary>
    /// Delete a profile
    /// </summary>
    public Task<bool> DeleteProfileAsync(string profileName)
    {
        string filePath = GetProfilePath(profileName);
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return Task.FromResult(true);
        }
        
        return Task.FromResult(false);
    }

    /// <summary>
    /// Check if a profile exists
    /// </summary>
    public bool ProfileExists(string profileName)
    {
        return File.Exists(GetProfilePath(profileName));
    }

    /// <summary>
    /// Get the profiles directory path
    /// </summary>
    public string GetProfilesDirectory()
    {
        return _profilesDirectory;
    }
}
