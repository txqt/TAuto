using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core.Models;
using TAuto.Core;
using NCrontab;

namespace TAuto.Automation.Services;

public class SchedulerService : IDisposable
{
    private readonly List<ScheduledJob> _jobs = new();
    private readonly Timer _timer;
    private bool _isRunning;
    
    public event EventHandler<string>? OnLog;
    public event EventHandler<ScheduledJob>? JobTriggered;

    public SchedulerService()
    {
        _timer = new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
        LoadJobs();
    }
    
    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        
        // Recalculate next run times
        foreach(var job in _jobs) UpdateNextRunTime(job);
        
        _timer.Change(0, 10000); 
        Log("Scheduler Service Started");
    }
    // ...
    // Methods to add/remove jobs also save
    public void AddJob(ScheduledJob job)
    {
        _jobs.Add(job);
        UpdateNextRunTime(job);
        SaveJobs();
    }
    
    public void UpdateJob(ScheduledJob job)
    {
        var existing = _jobs.FirstOrDefault(j => j.Id == job.Id);
        if (existing != null) _jobs.Remove(existing);
        _jobs.Add(job);
        UpdateNextRunTime(job);
        SaveJobs();
    }
    
    public void RemoveJob(string jobId)
    {
        var existing = _jobs.FirstOrDefault(j => j.Id == jobId);
        if (existing != null) 
        {
            _jobs.Remove(existing);
            SaveJobs();
        }
    }

    private void SaveJobs()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string path = System.IO.Path.Combine(appData, "AutoBot", "jobs.json");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            
            string json = System.Text.Json.JsonSerializer.Serialize(_jobs, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(path, json);
        }
        catch (Exception ex) { Log($"Failed to save jobs: {ex.Message}"); }
    }
    
    private void LoadJobs()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string path = System.IO.Path.Combine(appData, "AutoBot", "jobs.json");
            
            if (System.IO.File.Exists(path))
            {
                string json = System.IO.File.ReadAllText(path);
                var jobs = System.Text.Json.JsonSerializer.Deserialize<List<ScheduledJob>>(json);
                if (jobs != null)
                {
                    _jobs.Clear();
                    _jobs.AddRange(jobs);
                    Log($"Loaded {_jobs.Count} jobs.");
                }
            }
        }
        catch (Exception ex) { Log($"Failed to load jobs: {ex.Message}"); }
    }
    
    public void Stop()
    {
        _isRunning = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        Log("Scheduler Service Stopped");
    }
    
    
    public List<ScheduledJob> GetJobs() => _jobs.ToList();

    private void OnTick(object? state)
    {
        if (!_isRunning) return;
        
        var now = DateTime.Now;
        
        foreach (var job in _jobs)
        {
            if (!job.IsEnabled) continue;
            
            if (job.NextRunTime <= now)
            {
                // Trigger Execution
                Log($"ðŸš€ Triggering Job: {job.Name}");
                
                JobTriggered?.Invoke(this, job);
                
                job.LastRunTime = now;
                UpdateNextRunTime(job);
            }
        }
    }
    
    private void UpdateNextRunTime(ScheduledJob job)
    {
        var now = DateTime.Now;
        
        if (job.ScheduleType == ScheduleType.Interval)
        {
            var baseTime = job.LastRunTime ?? now;
            job.NextRunTime = baseTime.AddMinutes(job.IntervalMinutes);
            
            if (job.NextRunTime <= now)
            {
                job.NextRunTime = now.AddMinutes(job.IntervalMinutes);
            }
        }
        else if (job.ScheduleType == ScheduleType.Cron)
        {
            try
            {
                var schedule = CrontabSchedule.Parse(job.CronExpression);
                job.NextRunTime = schedule.GetNextOccurrence(now);
            }
            catch (Exception ex)
            {
                Log($"Error parsing Cron for job {job.Name}: {ex.Message}");
                job.IsEnabled = false;
                return;
            }
        }

        // Fleet de-correlation: apply Gaussian phase shift
        if (job.StartupVarianceMinutes > 0 && job.NextRunTime.HasValue)
        {
            var rng = new Random(job.Id.GetHashCode() ^ DateTime.Now.Ticks.GetHashCode());
            double u1 = 1.0 - rng.NextDouble();
            double u2 = 1.0 - rng.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            double offsetMinutes = z * (job.StartupVarianceMinutes / 2.0); // stdDev = half variance
            job.NextRunTime = job.NextRunTime.Value.AddMinutes(offsetMinutes);
            // Don't schedule in the past
            if (job.NextRunTime <= now)
                job.NextRunTime = now.AddMinutes(Math.Abs(offsetMinutes) + 1);
        }
    }
    
    private void Log(string msg) => OnLog?.Invoke(this, msg);

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
