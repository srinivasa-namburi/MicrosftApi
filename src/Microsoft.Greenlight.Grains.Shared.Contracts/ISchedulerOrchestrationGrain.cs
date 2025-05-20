using Orleans;
using System;
using System.Threading.Tasks;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling;

public interface ISchedulerOrchestrationGrain : IGrainWithStringKey
{
    Task<bool> PingAsync();
    Task<bool> InitializeAsync();
    Task StartScheduledJobsAsync();
    Task StopScheduledJobsAsync();
    Task UpdateReminderAsync(string reminderName, TimeSpan dueTime);
}