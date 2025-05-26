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

    // Centralized set of valid reminder names
    private static readonly HashSet<string> ValidReminderNames = new()
    {
        ContentReferenceIndexingReminder,
        PromptDefinitionsUpdateReminder,
        BlobAutoImportReminder,
        RepositoryIndexMaintenanceReminder
    };

    public SchedulerOrchestrationGrain(
        ILogger<SchedulerOrchestrationGrain> logger,
        IOptions<ServiceConfigurationOptions> options)
    {
        _logger = logger;
        _serviceConfigOptions = options.Value;
    }

    public Task<bool> PingAsync()
    {
        return Task.FromResult(true);
    }

    public async Task<bool> InitializeAsync()
    {
        // If already initialized, return success
        if (_isInitialized)
        {
            return true;
        }

        try
        {
            // Mark that we're initialized to avoid multiple executions
            _isInitialized = true;

            // Ensure legacy reminders are cleaned up
            await CleanupLegacyRemindersAsync();

            // Execute jobs directly (not in background task) to preserve Orleans threading context
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

            // Load existing reminders safely - the bare minimum to allow reminder handling
            await LoadExistingRemindersAsync(cancellationToken);

            // Auto-start schedulers if needed (but don't execute jobs immediately here)
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
            // Don't rethrow - allow grain activation to succeed even with errors
        }
    }

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
            // Don't throw - we'll recover by recreating reminders if needed
            // Initialize with empty dictionary to avoid null reference issues
            _reminders = new Dictionary<string, IGrainReminder>();
        }
    }

    /// <summary>
    /// Executes all scheduled jobs immediately, not waiting for their next scheduled run.
    /// </summary>
    private async Task ExecuteJobsImmediatelyAsync()
    {
        try
        {
            _logger.LogInformation("Executing all scheduled jobs immediately");

            // Execute all jobs using the same methods that reminders use
            await ExecuteContentReferenceIndexingJobAsync();
            await ExecutePromptDefinitionsUpdateJobAsync();
            await ExecuteRepositoryIndexMaintenanceJobAsync();

            _logger.LogInformation("All jobs have been executed immediately on startup");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing jobs immediately on startup");
            // Don't rethrow - we don't want to fail startup if immediate execution fails
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
        var referenceIndexingRefreshIntervalMinutes = _serviceConfigOptions.GreenlightServices.ReferenceIndexing.RefreshIntervalMinutes;
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

        // Blob Auto Import - every minute initially (gets altered by the job itself).
        await RegisterOrUpdateReminderAsync(
            BlobAutoImportReminder,
            TimeSpan.FromMinutes(1));

        // Repository Index Maintenance - every 2 minutes
        await RegisterOrUpdateReminderAsync(
            RepositoryIndexMaintenanceReminder,
            TimeSpan.FromMinutes(2));
    }

    public async Task UpdateReminderAsync(string reminderName, TimeSpan dueTime)
    {
        await _reminderLock.WaitAsync();
        try
        {
            if (!_reminders.TryGetValue(reminderName, out var existingReminder))
            {
                _logger.LogWarning("Attempted to update non-existent reminder {ReminderName}. Creating a new reminder instead.", reminderName);
                
                // Register a new reminder since it doesn't exist
                try
                {
                    var reminder = await this.RegisterOrUpdateReminder(
                        reminderName,
                        dueTime,
                        dueTime);

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
                
                var reminder = await this.RegisterOrUpdateReminder(
                    reminderName,
                    dueTime,
                    dueTime);

                _reminders[reminderName] = reminder;

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
        // Only run if there are reminders in state - otherwise silently exit
        if (_reminders.Count == 0)
        {
            _logger.LogInformation("No reminders to stop. Exiting StopScheduledJobsAsync.");
            return;
        }

        await _reminderLock.WaitAsync();
        try
        {
            _logger.LogInformation("Stopping all scheduled jobs");

            // Create a copy of the keys to avoid modifying the dictionary while iterating
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
                    
                    // Remove from our tracking dictionary after successful unregistering
                    _reminders.Remove(reminderName);
                }
                catch (Exception ex) when (ex.Message.Contains("Resource not found") || 
                                          ex.Message.Contains("not exist") || 
                                          ex.Message.Contains("tag mismatch"))
                {
                    // Handle specifically the case where the reminder doesn't exist in the backend
                    _logger.LogWarning(ex, "Reminder {ReminderName} doesn't exist in Azure Storage or had tag mismatch. Removing from local state only.", reminderName);
                    _reminders.Remove(reminderName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error unregistering reminder {ReminderName}", reminderName);
                    // Continue with other reminders even if one fails
                }
            }

            // Clear any remaining reminders from the dictionary
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

            if (reminderExists)
            {
                _logger.LogInformation("Reminder {ReminderName} already exists. Skipping registration.", reminderName);
                return;
            }

            _logger.LogInformation("Registering new reminder {ReminderName} with due time {DueTime}",
                reminderName, dueTime);

            try
            {
                // Use exponential backoff to retry reminder registration
                Exception? lastException = null;
                for (int retry = 0; retry < 3; retry++)
                {
                    try
                    {
                        _logger.LogDebug("Attempting to register reminder {ReminderName} (attempt {Attempt}/3)", reminderName, retry + 1);
                        
                        var reminder = await this.RegisterOrUpdateReminder(
                            reminderName,
                            TimeSpan.Zero, // Run immediately for the first time
                            dueTime);      // Then follow the regular schedule

                        _reminders[reminderName] = reminder;
                        
                        _logger.LogInformation("Successfully registered reminder {ReminderName}", reminderName);
                        return; // Success - exit the method
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

                // If we get here, all retries failed
                _logger.LogError(lastException, "Failed to register reminder {ReminderName} after multiple attempts", reminderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register reminder {ReminderName}", reminderName);
            }
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
                // Use centralized set for switch
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
                default:
                    // If reminder name is not recognized, it's likely a legacy reminder
                    // that should be unregistered
                    _logger.LogWarning("Unrecognized reminder received: {ReminderName}. Attempting to unregister it.", reminderName);
                    await UnregisterLegacyReminderAsync(reminderName);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing reminder {ReminderName}", reminderName);
            // Never throw from ReceiveReminder - Orleans doesn't handle these exceptions well
        }
    }

    /// <summary>
    /// Unregisters a legacy reminder that is no longer needed.
    /// </summary>
    /// <param name="reminderName">The name of the reminder to unregister.</param>
    private async Task UnregisterLegacyReminderAsync(string reminderName)
    {
        await _reminderLock.WaitAsync();
        try
        {
            // Check if the reminder is in our local dictionary
            if (_reminders.TryGetValue(reminderName, out var existingReminder))
            {
                try
                {
                    _logger.LogInformation("Unregistering legacy reminder: {ReminderName}", reminderName);
                    await this.UnregisterReminder(existingReminder);
                    
                    // Remove from local state after successful unregistering
                    _reminders.Remove(reminderName);
                    _logger.LogInformation("Successfully unregistered legacy reminder: {ReminderName}", reminderName);
                }
                catch (Exception ex) when (ex.Message.Contains("Resource not found") || 
                                          ex.Message.Contains("not exist") || 
                                          ex.Message.Contains("tag mismatch"))
                {
                    // Handle specifically the case where the reminder doesn't exist in the backend
                    _logger.LogWarning(ex, "Legacy reminder {ReminderName} doesn't exist in Azure Storage or had tag mismatch. Removing from local state only.", reminderName);
                    _reminders.Remove(reminderName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error unregistering legacy reminder {ReminderName}", reminderName);
                }
            }
            else
            {
                // If the reminder is not in our dictionary but was triggered,
                // we need to find and unregister it directly
                try
                {
                    _logger.LogInformation("Legacy reminder {ReminderName} not found in local state. Attempting to find and unregister it.", reminderName);
                    
                    // Get all reminders for this grain
                    var reminders = await this.GetReminders();
                    var reminderToRemove = reminders.FirstOrDefault(r => r.ReminderName == reminderName);
                    
                    if (reminderToRemove != null)
                    {
                        await this.UnregisterReminder(reminderToRemove);
                        _logger.LogInformation("Successfully unregistered legacy reminder {ReminderName} directly from storage.", reminderName);
                    }
                    else
                    {
                        _logger.LogWarning("Could not find legacy reminder {ReminderName} to unregister. It may have already been removed.", reminderName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error finding or unregistering legacy reminder {ReminderName} directly", reminderName);
                }
            }
        }
        finally
        {
            _reminderLock.Release();
        }
    }

    /// <summary>
    /// Proactively identifies and removes any legacy reminders that are no longer needed.
    /// </summary>
    private async Task CleanupLegacyRemindersAsync()
    {
        try
        {
            _logger.LogInformation("Checking for legacy reminders to clean up");
            
            // Use centralized set of valid reminder names
            var legacyReminders = _reminders.Keys
                .Where(name => !ValidReminderNames.Contains(name))
                .ToList();
                
            if (legacyReminders.Count > 0)
            {
                _logger.LogInformation("Found {Count} legacy reminders to unregister: {Reminders}", 
                    legacyReminders.Count, string.Join(", ", legacyReminders));
                    
                // Unregister each legacy reminder
                foreach (var reminderName in legacyReminders)
                {
                    await UnregisterLegacyReminderAsync(reminderName);
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
            // Don't rethrow - this is a maintenance operation
        }
    }
}
