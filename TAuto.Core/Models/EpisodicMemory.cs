using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace TAuto.Core.Models;

/// <summary>
/// Ring-buffer memory of recent bot actions and outcomes.
/// Used to detect repetitive failure loops and inform adaptive behavior.
/// Thread-safe via ConcurrentQueue. Cap = 50 records.
/// </summary>
public class EpisodicMemory
{
    public const int MaxCapacity = 50;

    private readonly ConcurrentQueue<Episode> _episodes = new();

    public int Count => _episodes.Count;

    public void Record(string stateName, string actionName, bool success)
    {
        _episodes.Enqueue(new Episode
        {
            StateName = stateName,
            ActionName = actionName,
            Success = success,
            Timestamp = DateTime.UtcNow
        });

        // Evict oldest when over capacity
        while (_episodes.Count > MaxCapacity)
            _episodes.TryDequeue(out _);
    }

    /// <summary>
    /// Count consecutive failures for a given state (most recent first).
    /// </summary>
    public int ConsecutiveFailures(string stateName)
    {
        int count = 0;
        foreach (var ep in _episodes.Reverse())
        {
            if (ep.StateName != stateName) break;
            if (!ep.Success) count++;
            else break;
        }
        return count;
    }

    /// <summary>
    /// Get all episodes (snapshot).
    /// </summary>
    public Episode[] GetAll() => _episodes.ToArray();

    /// <summary>
    /// Calculate action entropy (Shannon entropy of state distribution).
    /// Low entropy = repetitive bot-like behavior.
    /// </summary>
    public double CalculateEntropy()
    {
        var snapshot = _episodes.ToArray();
        if (snapshot.Length == 0) return 0;

        var groups = snapshot.GroupBy(e => e.StateName).ToArray();
        double total = snapshot.Length;
        double entropy = 0;
        foreach (var g in groups)
        {
            double p = g.Count() / total;
            if (p > 0) entropy -= p * Math.Log2(p);
        }
        return entropy;
    }

    public void Clear() 
    { 
        while (_episodes.TryDequeue(out _)) { } 
    }
}

public struct Episode
{
    public string StateName { get; set; }
    public string ActionName { get; set; }
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; }
}
