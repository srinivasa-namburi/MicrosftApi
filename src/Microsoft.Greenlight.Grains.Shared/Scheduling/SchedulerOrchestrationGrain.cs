using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Orleans.Concurrency;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling;

[Reentrant]
public class SchedulerOrchestrationGrain : Grain, ISchedulerOrchestrationGrain, IRemindable
{
    private readonly ILogger<SchedulerOrchestrationGrain> _logger;
    private readonly IOptionsSnapshot<ServiceConfigurationOptions> _optionsSnapshot;
    private Dictionary<string, IGrainReminder> _reminders = new();
    private readonly SemaphoreSlim _reminderLock = new(1, 1);

    // Define reminder names as constants to avoid typos
    private const string ContentReferenceIndexingReminder = "ContentReferenceIndexing";
    private const string PromptDefinitionsUpdateReminder = "PromptDefinitionsUpdate";
    private const string RepositoryIndexMaintenanceReminder = "RepositoryIndexMaintenance";
    private const string BlobAutoImportReminder = "BlobAutoImport";

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

            // Always execute all jobs immediately on startup, regardless of whether reminders existed
            _logger.LogInformation("Executing all scheduled jobs immediately upon startup");
            await ExecuteJobsImmediatelyAsync();

            // Load existing reminders
            await LoadExistingRemindersAsync(cancellationToken);

            // Auto-start schedulers if needed
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

    private async Task LoadExistingRemindersAsync(CancellationToken cancellationToken)
    {
        var reminders = await this.GetReminders();
        _reminders.Clear();

        foreach (var reminder in reminders)
        {
            _reminders[reminder.ReminderName] = reminder;
            _logger.LogDebug("Found existing reminder: {ReminderName}", reminder.ReminderName);
        }
    }

    /// <summary>
    /// Executes all scheduled jobs immediately, not waiting for their next scheduled run.
    /// </summary>
    private async Task ExecuteJobsImmediatelyAsync()
    {
        try
        {
            // Execute all jobs using the same methods that reminders use
            await ExecuteContentReferenceIndexingJobAsync();
            await ExecutePromptDefinitionsUpdateJobAsync();
            await ExecuteRepositoryIndexMaintenanceJobAsync();
            await ExecuteBlobAutoImportJobAsync();

            _logger.LogInformation("All jobs have been executed immediately on startup");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing jobs immediately on startup");
            // Don't rethrow - we don't want to fail startup if immediate execution fails
        }
    }

    /// <summary>
    /// Executes the Content Reference Indexing job.
    /// This method is used both by the reminder and for immediate execution.
    /// </summary>
    private async Task ExecuteContentReferenceIndexingJobAsync()
    {
        try
        {
            _logger.LogInformation("Executing Content Reference Indexing job");
            var grain = GrainFactory.GetGrain<IContentReferenceIndexingGrain>(Guid.NewGuid());
            await grain.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Content Reference Indexing job");
        }
    }

    /// <summary>
    /// Executes the Prompt Definitions Update job.
    /// This method is used both by the reminder and for immediate execution.
    /// </summary>
    private async Task ExecutePromptDefinitionsUpdateJobAsync()
    {
        try
        {
            _logger.LogInformation("Executing Prompt Definitions Update job");
            var grain = GrainFactory.GetGrain<IPromptDefinitionsUpdateGrain>(Guid.NewGuid());
            await grain.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Prompt Definitions Update job");
        }
    }

    /// <summary>
    /// Executes the Repository Index Maintenance job.
    /// This method is used both by the reminder and for immediate execution.
    /// </summary>
    private async Task ExecuteRepositoryIndexMaintenanceJobAsync()
    {
        try
        {
            _logger.LogInformation("Executing Repository Index Maintenance job");
            var grain = GrainFactory.GetGrain<IRepositoryIndexMaintenanceGrain>(Guid.NewGuid());
            await grain.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Repository Index Maintenance job");
        }
    }

    /// <summary>
    /// Executes the Blob Auto Import job.
    /// This method is used both by the reminder and for immediate execution.
    /// </summary>
    private async Task ExecuteBlobAutoImportJobAsync()
    {
        try
        {
            _logger.LogInformation("Executing Blob Auto Import job");
            var grain = GrainFactory.GetGrain<IBlobAutoImportGrain>(Guid.NewGuid());
            await grain.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Blob Auto Import job");
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

        await RegisterOrUpdateReminderAsync(
            ContentReferenceIndexingReminder,
            TimeSpan.FromMinutes(referenceIndexingRefreshIntervalMinutes));

        // Prompt Definitions Update - every hour
        await RegisterOrUpdateReminderAsync(
            PromptDefinitionsUpdateReminder,
            TimeSpan.FromHours(1));

        // Repository Index Maintenance - every minute
        await RegisterOrUpdateReminderAsync(
            RepositoryIndexMaintenanceReminder,
            TimeSpan.FromMinutes(1));

        // Blob Auto Import - every minute initially (gets altered by the job itself).
        await RegisterOrUpdateReminderAsync(
            BlobAutoImportReminder,
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

    /// <summary>
    /// Registers or updates a reminder with the specified name and due time.
    /// If the reminder already exists with the same due time, it is not modified.
    /// </summary>
    private async Task RegisterOrUpdateReminderAsync(string reminderName, TimeSpan dueTime)
    {
        await _reminderLock.WaitAsync();
        try
        {
            // Check if reminder already exists
            bool reminderExists = _reminders.TryGetValue(reminderName, out var existingReminder);

            // If it doesn't exist, or if the period has changed, register/update it
            if (!reminderExists)
            {
                _logger.LogInformation("Registering new reminder {ReminderName} with due time {DueTime}",
                    reminderName, dueTime);

                var reminder = await this.RegisterOrUpdateReminder(
                    reminderName,
                    TimeSpan.Zero, // Run immediately for the first time
                    dueTime);      // Then follow the regular schedule

                _reminders[reminderName] = reminder;
            }
        }
        finally
        {
            _reminderLock.Release();
        }
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        _logger.LogDebug("Reminder triggered: {ReminderName}", reminderName);

        // Call the appropriate job execution method based on the reminder name
        switch (reminderName)
        {
            case ContentReferenceIndexingReminder:
                await ExecuteContentReferenceIndexingJobAsync();
                break;

            case PromptDefinitionsUpdateReminder:
                await ExecutePromptDefinitionsUpdateJobAsync();
                break;

            case RepositoryIndexMaintenanceReminder:
                await ExecuteRepositoryIndexMaintenanceJobAsync();
                break;

            case BlobAutoImportReminder:
                await ExecuteBlobAutoImportJobAsync();
                break;

            default:
                _logger.LogWarning("Unknown reminder name: {ReminderName}", reminderName);
                break;
        }
    }
}
