using Orleans;

public interface ISchedulerOrchestrationGrain : IGrainWithStringKey
{
    Task StartScheduledJobsAsync();
    Task StopScheduledJobsAsync();
    Task UpdateReminderAsync(string reminderName, TimeSpan dueTime);
    Task<bool> PingAsync(); // Add this method
}