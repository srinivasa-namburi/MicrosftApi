// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling;

[Reentrant]
public class SchedulerOrchestrationGrain : Grain, ISchedulerOrchestrationGrain, IRemindable
{
    private readonly ILogger<SchedulerOrchestrationGrain> _logger;
    private readonly ServiceConfigurationOptions _serviceConfigOptions;
    private Dictionary<string, IGrainReminder> _reminders = new();
    private readonly SemaphoreSlim _reminderLock = new(1, 1);
    private bool _isInitialized = false;

    // Define reminder names as constants to avoid typos
    private const string ContentReferenceIndexingReminder = "ContentReferenceIndexing";
    private const string PromptDefinitionsUpdateReminder = "PromptDefinitionsUpdate";
    private const string BlobAutoImportReminder = "BlobAutoImport";
    private const string RepositoryIndexMaintenanceReminder = "RepositoryIndexMaintenance";
    private const string VectorStoreIdFixReminder = "VectorStoreIdFix"; // monthly heavy job

    // Centralized set of valid reminder names
    private static readonly HashSet<string> ValidReminderNames = new()
    {
        ContentReferenceIndexingReminder,
        PromptDefinitionsUpdateReminder,
        BlobAutoImportReminder,
        RepositoryIndexMaintenanceReminder,
        VectorStoreIdFixReminder
    };

    // Heavy job running flag
    private volatile bool _vectorStoreIdFixRunning = false;

    public SchedulerOrchestrationGrain(
        ILogger<SchedulerOrchestrationGrain> logger,
        IOptions<ServiceConfigurationOptions> options)
    {
        _logger = logger;
        _serviceConfigOptions = options.Value;
    }

    public Task<bool> PingAsync() => Task.FromResult(true);

    public async Task<bool> InitializeAsync()
    {
        if (_isInitialized)
        {
            return true;
        }

        try
        {
            _isInitialized = true;

            await CleanupLegacyRemindersAsync();
            await ExecuteJobsImmediatelyAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during grain initialization");
            _isInitialized = false;
            return false;
        }
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await base.OnActivateAsync(cancellationToken);
            _logger.LogInformation("SchedulerOrchestrationGrain activated with primary key {Key}", this.GetPrimaryKeyString());

            await LoadExistingRemindersAsync(cancellationToken);

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
        }
    }

    public Task<bool> IsVectorStoreIdFixRunningAsync() => Task.FromResult(_vectorStoreIdFixRunning);

    private async Task LoadExistingRemindersAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Loading existing reminders");
            var reminders = await this.GetReminders();
            _logger.LogInformation("Found {Count} existing reminders", reminders.Count);
            _reminders.Clear();
            foreach (var reminder in reminders)
            {
                _reminders[reminder.ReminderName] = reminder;
                _logger.LogInformation("Found existing reminder: {ReminderName}", reminder.ReminderName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading existing reminders");
            _reminders = new Dictionary<string, IGrainReminder>();
        }
    }

    private async Task ExecuteJobsImmediatelyAsync()
    {
        try
        {
            _logger.LogInformation("Executing all scheduled jobs immediately");

            await ExecuteContentReferenceIndexingJobAsync();
            await ExecutePromptDefinitionsUpdateJobAsync();
            await ExecuteRepositoryIndexMaintenanceJobAsync();

            if (_serviceConfigOptions.GreenlightServices.Global.EnableVectorStoreIdFixJob)
            {
                await ExecuteVectorStoreIdFixJobAsync();
            }
            else
            {
                _logger.LogInformation("Vector Store ID Fix job is disabled by configuration");
            }

            _logger.LogInformation("All jobs have been executed immediately on startup");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing jobs immediately on startup");
        }
    }

    private async Task ExecuteVectorStoreIdFixJobAsync()
    {
        try
        {
            if (_vectorStoreIdFixRunning)
            {
                _logger.LogInformation("Vector Store ID Fix job is already running. Skipping duplicate execution.");
                return;
            }

            _vectorStoreIdFixRunning = true;
            _logger.LogInformation("Executing Vector Store ID Fix job");
            var grain = GrainFactory.GetGrain<Microsoft.Greenlight.Grains.Shared.Contracts.IVectorStoreIdFixGrain>(Guid.NewGuid());
            await grain.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Vector Store ID Fix job");
        }
        finally
        {
            _vectorStoreIdFixRunning = false;
            _logger.LogInformation("Vector Store ID Fix job finished");
        }
    }

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

    private async Task ExecuteBlobAutoImportJobAsync()
    {
        try
        {
            // If the heavy fix is running, skip auto import run to reduce pressure
            if (_serviceConfigOptions.GreenlightServices.Global.EnableVectorStoreIdFixJob && _vectorStoreIdFixRunning)
            {
                _logger.LogInformation("Skipping Blob Auto Import job while Vector Store ID Fix job is running");
                return;
            }

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

        var referenceIndexingRefreshIntervalMinutes = _serviceConfigOptions.GreenlightServices.ReferenceIndexing.RefreshIntervalMinutes;
        if (referenceIndexingRefreshIntervalMinutes < 1)
        {
            referenceIndexingRefreshIntervalMinutes = 1;
        }

        await RegisterOrUpdateReminderAsync(ContentReferenceIndexingReminder, TimeSpan.FromMinutes(referenceIndexingRefreshIntervalMinutes));
        await RegisterOrUpdateReminderAsync(PromptDefinitionsUpdateReminder, TimeSpan.FromHours(1));
        await RegisterOrUpdateReminderAsync(BlobAutoImportReminder, TimeSpan.FromMinutes(1));
        await RegisterOrUpdateReminderAsync(RepositoryIndexMaintenanceReminder, TimeSpan.FromMinutes(2));

        if (_serviceConfigOptions.GreenlightServices.Global.EnableVectorStoreIdFixJob)
        {
            await RegisterOrUpdateReminderAsync(VectorStoreIdFixReminder, TimeSpan.FromDays(30));
        }
        else
        {
            _logger.LogInformation("Vector Store ID Fix monthly reminder not registered because it is disabled by configuration");
        }
    }

    public async Task UpdateReminderAsync(string reminderName, TimeSpan dueTime)
    {
        await _reminderLock.WaitAsync();
        try
        {
            if (!_reminders.TryGetValue(reminderName, out var existingReminder))
            {
                _logger.LogWarning("Attempted to update non-existent reminder {ReminderName}. Creating a new reminder instead.", reminderName);
                try
                {
                    var reminder = await this.RegisterOrUpdateReminder(reminderName, dueTime, dueTime);
                    _reminders[reminderName] = reminder;
                    _logger.LogInformation("Created new reminder {ReminderName} with due time {DueTime}", reminderName, dueTime);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create new reminder {ReminderName}", reminderName);
                }
                return;
            }

            try
            {
                _logger.LogInformation("Updating existing reminder {ReminderName} with new due time {DueTime}", reminderName, dueTime);
                var updated = await this.RegisterOrUpdateReminder(reminderName, dueTime, dueTime);
                _reminders[reminderName] = updated;
                _logger.LogInformation("Successfully updated reminder {ReminderName}", reminderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update reminder {ReminderName}", reminderName);
            }
        }
        finally
        {
            _reminderLock.Release();
        }
    }

    public async Task StopScheduledJobsAsync()
    {
        if (_reminders.Count == 0)
        {
            _logger.LogInformation("No reminders to stop. Exiting StopScheduledJobsAsync.");
            return;
        }

        await _reminderLock.WaitAsync();
        try
        {
            _logger.LogInformation("Stopping all scheduled jobs");
            var reminderNames = new List<string>(_reminders.Keys);
            foreach (var reminderName in reminderNames)
            {
                if (!_reminders.TryGetValue(reminderName, out var reminder))
                {
                    _logger.LogWarning("Reminder {ReminderName} was in state but not found in dictionary during stop operation", reminderName);
                    continue;
                }

                try
                {
                    _logger.LogInformation("Attempting to unregister reminder: {ReminderName}", reminderName);
                    await this.UnregisterReminder(reminder);
                    _logger.LogInformation("Successfully unregistered reminder: {ReminderName}", reminderName);
                    _reminders.Remove(reminderName);
                }
                catch (Exception ex) when (ex.Message.Contains("Resource not found") || ex.Message.Contains("not exist") || ex.Message.Contains("tag mismatch"))
                {
                    _logger.LogWarning(ex, "Reminder {ReminderName} doesn't exist in Azure Storage or had tag mismatch. Removing from local state only.");
                    _reminders.Remove(reminderName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error unregistering reminder {ReminderName}", reminderName);
                }
            }

            if (_reminders.Count > 0)
            {
                _logger.LogWarning("Clearing {Count} reminders that couldn't be unregistered", _reminders.Count);
                _reminders.Clear();
            }
        }
        finally
        {
            _reminderLock.Release();
        }
    }

    private async Task RegisterOrUpdateReminderAsync(string reminderName, TimeSpan dueTime)
    {
        await _reminderLock.WaitAsync();
        try
        {
            bool exists = _reminders.TryGetValue(reminderName, out var existingReminder);
            if (exists)
            {
                _logger.LogInformation("Reminder {ReminderName} already exists. Skipping registration.", reminderName);
                return;
            }

            _logger.LogInformation("Registering new reminder {ReminderName} with due time {DueTime}", reminderName, dueTime);

            Exception? lastException = null;
            for (int retry = 0; retry < 3; retry++)
            {
                try
                {
                    _logger.LogDebug("Attempting to register reminder {ReminderName} (attempt {Attempt}/3)", reminderName, retry + 1);
                    var reminder = await this.RegisterOrUpdateReminder(reminderName, TimeSpan.Zero, dueTime);
                    _reminders[reminderName] = reminder;
                    _logger.LogInformation("Successfully registered reminder {ReminderName}", reminderName);
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Failed to register reminder {ReminderName} (attempt {Attempt}/3)", reminderName, retry + 1);
                    if (retry < 2)
                    {
                        int delaySeconds = (int)Math.Pow(2, retry);
                        _logger.LogDebug("Waiting {DelaySeconds} seconds before retry", delaySeconds);
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                    }
                }
            }

            _logger.LogError(lastException, "Failed to register reminder {ReminderName} after multiple attempts", reminderName);
        }
        finally
        {
            _reminderLock.Release();
        }
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        try
        {
            _logger.LogDebug("Reminder triggered: {ReminderName}", reminderName);

            switch (reminderName)
            {
                case ContentReferenceIndexingReminder:
                    await ExecuteContentReferenceIndexingJobAsync();
                    break;
                case PromptDefinitionsUpdateReminder:
                    await ExecutePromptDefinitionsUpdateJobAsync();
                    break;
                case BlobAutoImportReminder:
                    await ExecuteBlobAutoImportJobAsync();
                    break;
                case RepositoryIndexMaintenanceReminder:
                    await ExecuteRepositoryIndexMaintenanceJobAsync();
                    break;
                case VectorStoreIdFixReminder:
                    if (_serviceConfigOptions.GreenlightServices.Global.EnableVectorStoreIdFixJob)
                    {
                        await ExecuteVectorStoreIdFixJobAsync();
                    }
                    else
                    {
                        _logger.LogInformation("Vector Store ID Fix reminder triggered but job is disabled by configuration. Ignoring.");
                    }
                    break;
                default:
                    _logger.LogWarning("Unrecognized reminder received: {ReminderName}. Ignoring.", reminderName);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing reminder {ReminderName}", reminderName);
        }
    }

    private async Task CleanupLegacyRemindersAsync()
    {
        try
        {
            _logger.LogInformation("Checking for legacy reminders to clean up");
            var reminders = await this.GetReminders();
            var legacyReminders = reminders.Select(r => r.ReminderName).Where(name => !ValidReminderNames.Contains(name)).ToList();
            if (legacyReminders.Count > 0)
            {
                _logger.LogInformation("Found {Count} legacy reminders to unregister: {Reminders}", legacyReminders.Count, string.Join(", ", legacyReminders));
                foreach (var reminderName in legacyReminders)
                {
                    try
                    {
                        var toRemove = reminders.FirstOrDefault(r => r.ReminderName == reminderName);
                        if (toRemove != null)
                        {
                            await this.UnregisterReminder(toRemove);
                            _reminders.Remove(reminderName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error unregistering legacy reminder {ReminderName}", reminderName);
                    }
                }
            }
            else
            {
                _logger.LogInformation("No legacy reminders found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while cleaning up legacy reminders");
        }
    }
}
