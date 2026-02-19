using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using TAuto.Core.Models;

namespace TAuto.Automation.Services;

public class SessionManager
{
    private readonly string _sessionDir;
    
    public SessionManager()
    {
        // Save sessions in AppData or near executable
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _sessionDir = Path.Combine(appData, "AutoBot", "Sessions");
        Directory.CreateDirectory(_sessionDir);
    }
    
    public async Task SaveSessionAsync(SessionState session)
    {
        if (session == null) return;
        
        string filePath = Path.Combine(_sessionDir, $"{session.SessionId}.json");
        string json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
        
        await File.WriteAllTextAsync(filePath, json);
    }
    
    public async Task<SessionState?> LoadSessionAsync(string sessionId)
    {
        string filePath = Path.Combine(_sessionDir, $"{sessionId}.json");
        if (!File.Exists(filePath)) return null;
        
        try
        {
            string json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<SessionState>(json);
        }
        catch 
        {
            return null; 
        }
    }
    
    public void DeleteSession(string sessionId)
    {
        string filePath = Path.Combine(_sessionDir, $"{sessionId}.json");
        if (File.Exists(filePath)) File.Delete(filePath);
    }
    
    public List<SessionState> GetAllSessions()
    {
        var sessions = new List<SessionState>();
        var files = Directory.GetFiles(_sessionDir, "*.json");
        
        foreach (var file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                var session = JsonSerializer.Deserialize<SessionState>(json);
                if (session != null) sessions.Add(session);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SessionManager] Failed to load session: {ex.Message}"); }
        }
        
        return sessions.OrderByDescending(s => s.LastSaveTime).ToList();
    }
}
