using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling;

[Reentrant]
public class SchedulerOrchestrationGrain : Grain, ISchedulerOrchestrationGrain, IRemindable
{
    private readonly ILogger<SchedulerOrchestrationGrain> _logger;
    private readonly IOptionsSnapshot<ServiceConfigurationOptions> _optionsSnapshot;
    private Dictionary<string, IGrainReminder> _reminders = new();
    private readonly SemaphoreSlim _reminderLock = new(1, 1);

    public SchedulerOrchestrationGrain(
        ILogger<SchedulerOrchestrationGrain> logger,
        IOptionsSnapshot<ServiceConfigurationOptions> optionsSnapshot)
    {
        _logger = logger;
        _optionsSnapshot = optionsSnapshot;
    }

    public Task<bool> PingAsync()
    {
        return Task.FromResult(true);
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await base.OnActivateAsync(cancellationToken);

            _logger.LogInformation("SchedulerOrchestrationGrain activated with primary key {Key}", this.GetPrimaryKeyString());

            // Keep track of this activation in a persistent state
            // Auto-start schedulers if not already started
            if (_reminders.Count == 0)
            {
                _logger.LogInformation("No reminders found. Starting scheduled jobs...");
                await StartScheduledJobsAsync();
            }
            else
            {
                _logger.LogInformation("Found {Count} existing reminders. Not re-registering.", _reminders.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnActivateAsync for SchedulerOrchestrationGrain");
            throw; // Re-throw to ensure Orleans knows the activation failed
        }
    }

    public async Task StartScheduledJobsAsync()
    {
        _logger.LogInformation("Starting all scheduled jobs");

        // Content Reference Indexing - every X minutes from config
        var referenceIndexingRefreshIntervalMinutes = _optionsSnapshot.Value.GreenlightServices.ReferenceIndexing.RefreshIntervalMinutes;
        if (referenceIndexingRefreshIntervalMinutes < 1)
        {
            referenceIndexingRefreshIntervalMinutes = 1; // Ensure at least 1 minute
        }

        await RegisterReminderAsync("ContentReferenceIndexing",
            TimeSpan.FromMinutes(referenceIndexingRefreshIntervalMinutes));

        // Prompt Definitions Update - every hour
        await RegisterReminderAsync("PromptDefinitionsUpdate",
            TimeSpan.FromHours(1));

        // Repository Index Maintenance - every minute
        await RegisterReminderAsync("RepositoryIndexMaintenance",
            TimeSpan.FromMinutes(1));

        // Blob Auto Import - every 30 seconds initially
        await RegisterReminderAsync("BlobAutoImport",
            TimeSpan.FromMinutes(1));
    }

    public async Task UpdateReminderAsync(string reminderName, TimeSpan dueTime)
    {
        await _reminderLock.WaitAsync();
        try
        {
            if (!_reminders.ContainsKey(reminderName))
            {
                _logger.LogWarning("Attempted to update non-existent reminder {ReminderName}", reminderName);
                return;
            }

            var reminder = await this.RegisterOrUpdateReminder(
                reminderName,
                dueTime,
                dueTime);

            _reminders[reminderName] = reminder;

            _logger.LogInformation("Updated reminder {ReminderName} with new due time {DueTime}",
                reminderName, dueTime);
        }
        finally
        {
            _reminderLock.Release();
        }
    }

    public async Task StopScheduledJobsAsync()
    {
        // Only run if there are reminders in state - otherwise silently exit
        if (_reminders.Count == 0)
        {
            return;
        }

        await _reminderLock.WaitAsync();
        try
        {
            _logger.LogInformation("Stopping all scheduled jobs");

            foreach (var reminder in _reminders.Values)
            {
                await this.UnregisterReminder(reminder);
            }

            _reminders.Clear();
        }
        finally
        {
            _reminderLock.Release();
        }
    }

    private async Task RegisterReminderAsync(string reminderName, TimeSpan dueTime)
    {
        await _reminderLock.WaitAsync();
        try
        {
            if (_reminders.ContainsKey(reminderName))
            {
                await this.UnregisterReminder(_reminders[reminderName]);
            }

            var reminder = await this.RegisterOrUpdateReminder(
                reminderName,
                dueTime,
                dueTime);

            _reminders[reminderName] = reminder;

            _logger.LogInformation("Registered reminder {ReminderName} with due time {DueTime}",
                reminderName, dueTime);
        }
        finally
        {
            _reminderLock.Release();
        }
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        _logger.LogDebug("Reminder triggered: {ReminderName}", reminderName);

        try
        {
            switch (reminderName)
            {
                case "ContentReferenceIndexing":
                    var contentReferenceGrain = GrainFactory.GetGrain<IContentReferenceIndexingGrain>(Guid.NewGuid());
                    await contentReferenceGrain.ExecuteAsync();
                    break;

                case "PromptDefinitionsUpdate":
                    var promptDefinitionsGrain = GrainFactory.GetGrain<IPromptDefinitionsUpdateGrain>(Guid.NewGuid());
                    await promptDefinitionsGrain.ExecuteAsync();
                    break;

                case "RepositoryIndexMaintenance":
                    var repositoryIndexGrain = GrainFactory.GetGrain<IRepositoryIndexMaintenanceGrain>(Guid.NewGuid());
                    await repositoryIndexGrain.ExecuteAsync();
                    break;

                case "BlobAutoImport":
                    var blobAutoImportGrain = GrainFactory.GetGrain<IBlobAutoImportGrain>(Guid.NewGuid());
                    await blobAutoImportGrain.ExecuteAsync();
                    break;

                default:
                    _logger.LogWarning("Unknown reminder name: {ReminderName}", reminderName);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing reminder {ReminderName}", reminderName);
        }
    }
}
