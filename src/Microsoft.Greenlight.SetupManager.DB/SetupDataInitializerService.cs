using System.Diagnostics;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.Components;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.Configuration;
using Microsoft.Greenlight.Shared.Models.Validation;
using System.Globalization;
using System.Text.Json;

namespace Microsoft.Greenlight.SetupManager.DB;

/// <summary>
/// The service used to setup the database and seed data.
/// </summary>
/// <param name="sp">The <see cref="IServiceProvider"/> for resolving dependencies.</param>
/// <param name="logger">The <see cref="ILogger"/> used for logging.</param>
/// <param name="lifetime">The <see cref="IHostApplicationLifetime"/> used for shutting down the application.</param>
public class SetupDataInitializerService(
    IServiceProvider sp,
    ILogger<SetupDataInitializerService> logger,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    /// <summary>
    /// The name of the activity source used for telemetry logging.
    /// </summary>
    public const string ActivitySourceName = "Migrations";

    private readonly IServiceProvider _sp = sp;
    private readonly ILogger<SetupDataInitializerService> _logger = logger;
    private readonly IHostApplicationLifetime _lifetime = lifetime;
    private readonly ActivitySource _activitySource = new(ActivitySourceName);

    private readonly Guid _gpt4OModelId = Guid.Parse("f7ece6e1-af11-4f90-a69f-c77fcef73486", CultureInfo.InvariantCulture);
    private readonly Guid _gpt4OModelDeploymentId = Guid.Parse("453a06c4-3ce8-4468-a7a8-7444f8352aa6", CultureInfo.InvariantCulture);
    private readonly Guid _o3MiniModelId = Guid.Parse("656d6371-8d78-4c4b-be7b-05254ff4045a", CultureInfo.InvariantCulture);

    /// <summary>
    /// The method that is called when the <see cref="SetupDataInitializerService"/> starts.
    /// </summary>
    /// <param name="cancellationToken">
    /// Triggered when <see cref="IHostedService.StopAsync(CancellationToken)"/> is called.</param>
    /// <returns>>A <see cref="Task"/> that represents the long running operations.</returns>
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = _sp.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<DocGenerationDbContext>();

        await InitializeDatabaseAsync(dbContext, cancellationToken);

        await SeedAsync(dbContext, cancellationToken);

        _lifetime.StopApplication();
    }

    private async Task InitializeDatabaseAsync(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        using var activity =
            _activitySource.StartActivity("Initializing Document Generation Database", ActivityKind.Client);
        var sw = Stopwatch.StartNew();

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(dbContext.Database.MigrateAsync, cancellationToken);
        sw.Stop();
        _logger.LogInformation(
            "Document Generation Database initialized in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);

        activity!.Stop();
    }

    private async Task SeedAsync(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        using var activity =
            _activitySource.StartActivity("Seeding Document Generation Database", ActivityKind.Client);
        _logger.LogInformation("Seeding Document Generation Database started");
        var sw = Stopwatch.StartNew();

        await Seed2024_04_07_IngestedDocumentDocumentProcess(dbContext, cancellationToken);
        await Seed2024_05_24_OrphanedChatMessagesCleanup(dbContext, cancellationToken);
        await Seed2024_05_24_ChatConversationsWithNoMessagesCleanup(dbContext, cancellationToken);
        await Seed2025_02_27_CreateDefaultSequentialValidationPipeline(dbContext, cancellationToken);
        await Seed2025_03_18_DefaultConfiguration(dbContext, cancellationToken);
        await Seed2025_04_24_AiModelSettings(dbContext, cancellationToken);
        await Seed2025_04_24_DefaultAiModelDeploymentForDocumentProcesses(dbContext, cancellationToken);
        
        sw.Stop();
        _logger.LogInformation(
            "Seeding Document Generation Database completed in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);

        activity!.Stop();
    }

    private async Task Seed2025_04_24_DefaultAiModelDeploymentForDocumentProcesses(DocGenerationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Get all DocumentProcesses that do not have an AiModelDeploymentId set
        var documentProcesses = await dbContext.DynamicDocumentProcessDefinitions
            .Where(x => x.AiModelDeploymentId == null || x.AiModelDeploymentForValidationId == null)
            .ToListAsync(cancellationToken);

        if (documentProcesses.Count == 0)
        {
            _logger.LogInformation(
                "No DocumentProcesses found without an AiModelDeploymentId. Skipping seeding logic.");
            return;
        }

        foreach (var documentProcess in documentProcesses)
        {
            // Set the default model to gpt-4o for the document process if it's not already set
            documentProcess.AiModelDeploymentId ??= _gpt4OModelDeploymentId;

            // Set the default validation model to gpt-4o for the document process if it's not already set
            documentProcess.AiModelDeploymentForValidationId ??= _gpt4OModelDeploymentId;

            // Track updates, if any

            if (dbContext.ChangeTracker.HasChanges())
            {
                dbContext.DynamicDocumentProcessDefinitions.Update(documentProcess);
            }

            _logger.LogInformation(@"Set default model to gpt-4o for document process {dpName}", documentProcess.ShortName);
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task Seed2025_04_24_AiModelSettings(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        // Check if the GPT-4o model exists
        var gpt4OAiModel = await dbContext.AiModels.FindAsync([_gpt4OModelId], cancellationToken);

        if (gpt4OAiModel == null)
        {
            gpt4OAiModel = new AiModel
            {
                Id = _gpt4OModelId,
                Name = "gpt-4o",
                TokenSettings = new AiModelMaxTokenSettings
                {
                    MaxTokensForContentGeneration = 8000,
                    MaxTokensForSummarization = 4000,
                    MaxTokensForValidation = 8000,
                    MaxTokensForChatReplies = 4000,
                    MaxTokensForQuestionAnswering = 4000,
                    MaxTokensGeneral = 1024
                },
                IsReasoningModel = false
            };

            dbContext.AiModels.Add(gpt4OAiModel);
        }
        else
        {
            _logger.LogInformation("GPT-4o model already exists. Skipping seeding logic.");
        }

        // Check if the GPT-4o model deployment exists
        var gpt4ODeployment = await dbContext.AiModelDeployments.FindAsync([_gpt4OModelDeploymentId], cancellationToken);

        if (gpt4ODeployment == null)
        {
            gpt4ODeployment = new AiModelDeployment
            {
                Id = _gpt4OModelDeploymentId,
                DeploymentName = "gpt-4o",
                AiModelId = _gpt4OModelId,
                TokenSettings = gpt4OAiModel.TokenSettings
            };

            dbContext.AiModelDeployments.Add(gpt4ODeployment);
        }
        else
        {
            _logger.LogInformation("GPT-4o model deployment already exists. Skipping seeding logic.");
        }

        // Check if the o3-mini model exists
        var o3MiniAiModel = await dbContext.AiModels.FindAsync([_o3MiniModelId], cancellationToken);
        if (o3MiniAiModel == null)
        {
            o3MiniAiModel = new AiModel
            {
                Id = _o3MiniModelId,
                Name = "o3-mini",
                TokenSettings = new AiModelMaxTokenSettings
                {
                    MaxTokensForContentGeneration = 90000,
                    MaxTokensForSummarization = 4000,
                    MaxTokensForValidation = 90000,
                    MaxTokensForChatReplies = 4000,
                    MaxTokensForQuestionAnswering = 7000,
                    MaxTokensGeneral = 1024
                },
                IsReasoningModel = true
            };
            dbContext.AiModels.Add(o3MiniAiModel);
        }
        else
        {
            _logger.LogInformation("o3-mini model already exists. Skipping seeding logic.");
        }

        // Only save changes if we've made changes
        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task Seed2025_02_27_CreateDefaultSequentialValidationPipeline(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        // For each DocumentProcess that does not have a default validation pipeline, create a default validation pipeline
        // with a single step that validates the document content

        var documentProcesses = await dbContext.DynamicDocumentProcessDefinitions
            .Where(x => x.ValidationPipelineId == null)
            .ToListAsync(cancellationToken);

        if (documentProcesses.Count == 0)
            return;

        _logger.LogInformation("Seeding : Creating default validation pipeline for {Count} DocumentProcesses", documentProcesses.Count);

        foreach (var dp in documentProcesses)
        {
            var pipeline = new DocumentProcessValidationPipeline
            {
                Id = Guid.NewGuid(),
                DocumentProcessId = dp.Id,
                ValidationPipelineSteps = new List<DocumentProcessValidationPipelineStep>
                {
                    new DocumentProcessValidationPipelineStep()
                    {
                        Id = Guid.NewGuid(),
                        DocumentProcessValidationPipelineId = dp.Id,
                        PipelineExecutionType = ValidationPipelineExecutionType.SequentialFullDocument
                    }
                }
            };
            
            dp.ValidationPipelineId = pipeline.Id;


            dbContext.DocumentProcessValidationPipelines.Add(pipeline);
            dbContext.DynamicDocumentProcessDefinitions.Update(dp);

        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeding : Created default validation pipeline for {Count} DocumentProcesses", documentProcesses.Count);
        
    }

    private async Task Seed2024_04_07_IngestedDocumentDocumentProcess(DocGenerationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Set Document Process to "US.NuclearLicensing" on IngestedDocuments where DocumentProcess is null
        // First, get a count of the number of IngestedDocuments where DocumentProcess is null. If it's 0, we don't
        // need to do anything.

        var count = await dbContext.IngestedDocuments
            .Where(x => x.DocumentProcess == null)
            .CountAsync(cancellationToken);

        if (count == 0)
        {
            _logger.LogInformation(
                "No IngestedDocuments found where DocumentProcess is null. Skipping seeding logic.");
            return;
        }


        _logger.LogInformation(
            "Seeding : Setting Document Process to 'US.NuclearLicensing' on {Count} IngestedDocuments where DocumentProcess is null",
            count);

        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE IngestedDocuments SET DocumentProcess = {0} WHERE DocumentProcess IS NULL",
            "US.NuclearLicensing");

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task Seed2024_05_24_OrphanedChatMessagesCleanup(DocGenerationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Remove any ChatMessages that are not associated with a ChatConversation
        // First, get a count of the number of ChatMessages that are not associated with a ChatConversation. If it's 0,
        // we don't need to do anything.

        // The ChatMessages currently have a ConversationId in the model that is not nullable, so we can't have a null
        // ConversationId.
        // Therefore, we need to execute a raw SQL query to find ChatMessages where the ConversationId is null

        var count = await dbContext.ChatMessages
            .FromSqlRaw("SELECT * FROM ChatMessages WHERE ConversationId IS NULL")
            .CountAsync(cancellationToken);

        if (count == 0)
        {
            _logger.LogInformation("No orphaned ChatMessages found. Skipping cleanup logic.");
            return;
        }

        _logger.LogInformation("Cleaning up : Removing {Count} orphaned ChatMessages", count);

        await dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM ChatMessages WHERE ConversationId IS NULL",
            Guid.Empty);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task Seed2024_05_24_ChatConversationsWithNoMessagesCleanup(DocGenerationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Remove any ChatConversations that have no associated ChatMessages
        // First, get a count of the number of ChatConversations that have no associated ChatMessages. If it's 0, we
        // don't need to do anything.

        // The ChatConversations currently have a list of ChatMessages in the model,
        // so we can use LINQ to find ChatConversations with no associated ChatMessages

        var expirePoint = DateTime.UtcNow - (7.Days());

        var count = await dbContext.ChatConversations
            .Where(x => x.ChatMessages.Count == 0)
            .Where(x => x.CreatedUtc < expirePoint)
            .CountAsync(cancellationToken);

        if (count == 0)
        {
            _logger.LogInformation(
                "No (old) ChatConversations found with no associated ChatMessages. Skipping cleanup logic.");
            return;
        }

        _logger.LogInformation(
            "Cleaning up : Removing {Count} ChatConversations with no associated ChatMessages that are older than 7 days",
            count);

        var chatConversations = await dbContext.ChatConversations
            .Where(x => x.ChatMessages.Count == 0)
            .Where(x => x.CreatedUtc < expirePoint)
            .ToListAsync(cancellationToken);

        // This marks the ChatConversations for deletion through their IsActive property
        dbContext.ChatConversations.RemoveRange(chatConversations);
        await dbContext.SaveChangesAsync(cancellationToken);

        // This actually deletes the ChatConversations from the database
        await dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM ChatConversations WHERE IsActive = 0", cancellationToken: cancellationToken);

    }

    private async Task Seed2025_03_18_DefaultConfiguration(DocGenerationDbContext dbContext,
     CancellationToken cancellationToken)
    {
        // Check if the default configuration record already exists
        var configExists = await dbContext.Configurations.AnyAsync(c => c.Id == 1, cancellationToken);

        const string keyName = "ServiceConfiguration:GreenlightServices:FrontEnd:SiteName";

        if (!configExists)
        {
            _logger.LogInformation("Creating default configuration record");

            // Create a dictionary with the default configuration values
            var configValues = new Dictionary<string, string>
            {
                [keyName] = "Generative AI for Permitting"
            };

            // Create the default configuration with the initial values
            var defaultConfig = new DbConfiguration
            {
                ConfigurationValues = JsonSerializer.Serialize(configValues),
                LastUpdated = DateTime.UtcNow,
                LastUpdatedBy = "System"
            };

            dbContext.Configurations.Add(defaultConfig);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Default configuration record created successfully");
        }
        else
        {
            // Check if we need to update existing configuration with the site name
            var config = await dbContext.Configurations.FirstAsync(c => c.Id == 1, cancellationToken);
            var configValues = JsonSerializer.Deserialize<Dictionary<string, string>>(
                config.ConfigurationValues,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Dictionary<string, string>();

            bool updated = false;

            // Add site name if it doesn't exist
            if (!configValues.ContainsKey(keyName))
            {
                configValues[keyName] = "Generative AI for Permitting";
                updated = true;
            }

            if (updated)
            {
                config.ConfigurationValues = JsonSerializer.Serialize(configValues);
                config.LastUpdated = DateTime.UtcNow;
                config.LastUpdatedBy = "System";

                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Updated existing configuration with site name");
            }
            else
            {
                _logger.LogInformation("Default configuration record already exists with required values. No updates needed.");
            }
        }
    }


}
