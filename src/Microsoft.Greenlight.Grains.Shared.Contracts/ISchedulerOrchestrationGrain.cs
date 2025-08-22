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
    /// <summary>
    /// Returns true while the Vector Store ID Fix job is running. False otherwise.
    /// </summary>
    Task<bool> IsVectorStoreIdFixRunningAsync();
}