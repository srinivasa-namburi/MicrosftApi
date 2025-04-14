using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.Components;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.Configuration;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Models.Validation;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using System.Diagnostics;
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

    private readonly Guid _nrcEnvironmentalReportId = Guid.Parse("88ffae0a-22a3-42e0-a538-72dd1a589216", CultureInfo.InvariantCulture);


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

        // Ensure prompt definitions are up-to-date
        var promptDefinitionService = _sp.GetRequiredService<IPromptDefinitionService>();
        await promptDefinitionService.EnsurePromptDefinitionsAsync(cancellationToken);

        await Seed2024_04_07_IngestedDocumentDocumentProcess(dbContext, cancellationToken);
        await Seed2024_05_24_OrphanedChatMessagesCleanup(dbContext, cancellationToken);
        await Seed2025_02_27_CreateDefaultSequentialValidationPipeline(dbContext, cancellationToken);
        await Seed2025_03_18_DefaultConfiguration(dbContext, cancellationToken);
        await Seed2025_04_24_AiModelSettings(dbContext, cancellationToken);
        await Seed2025_04_24_DefaultAiModelDeploymentForDocumentProcesses(dbContext, cancellationToken);
        
        await Seed2025_09_20_EnvironmentalReportDocumentProcess(dbContext, cancellationToken);

        sw.Stop();
        _logger.LogInformation(
            "Seeding Document Generation Database completed in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);

        activity!.Stop();
    }

    private async Task Seed2025_09_20_EnvironmentalReportDocumentProcess(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        const string documentProcessShortName = "NRC.EnvironmentalReview";

        // Check if the document process already exists
        var existingProcess = await dbContext.DynamicDocumentProcessDefinitions
            .FindAsync(_nrcEnvironmentalReportId, cancellationToken);

        if (existingProcess != null)
        {
            _logger.LogInformation("Environmental Report document process already exists. Skipping seeding.");
            return;
        }

        _logger.LogInformation("Seeding the US.NRC.EnvironmentalReport document process");

        // Create the document outline with full environmental report outline structure
        var documentOutline = new DocumentOutline();
        #region Full Outline Text
        documentOutline.FullText = """
                               1. Introduction
                               1.1. Project Overview
                               1.2. Applicant Information
                               1.3. Site Location
                               1.4. Regulatory Requirements
                               2. Environmental Description
                               2.1. Land Use and Geology
                               2.1.1. Topography
                               2.1.2. Soil Characteristics
                               2.1.3. Seismic Conditions
                               2.2. Water Resources
                               2.2.1. Surface Water
                               2.2.2. Groundwater
                               2.3. Ecology
                               2.3.1. Terrestrial Ecology
                               2.3.2. Aquatic Ecology
                               2.4. Climate and Meteorology
                               2.4.1. Local Climate
                               2.4.2. Meteorological Data
                               3. Plant Description
                               3.1. Plant Layout
                               3.2. Reactor Design
                               3.2.1. Reactor Core
                               3.2.2. Safety Systems
                               3.3. Auxiliary Systems
                               3.3.1. Cooling Systems
                               3.3.2. Waste Management Systems
                               4. Environmental Impacts of Construction
                               4.1. Land Disturbance
                               4.2. Air Quality
                               4.2.1. Dust Generation
                               4.2.2. Emissions from Equipment
                               4.3. Water Quality
                               4.4. Noise Levels
                               4.5. Waste Generation
                               4.5.1. Solid Waste
                               4.5.2. Hazardous Waste
                               5. Environmental Impacts of Station Operation
                               5.1. Air Quality
                               5.1.1. Routine Emissions
                               5.1.2. Accidental Releases
                               5.2. Water Quality
                               5.2.1. Thermal Discharge
                               5.2.2. Chemical Discharge
                               5.3. Land Use
                               5.4. Ecology
                               5.4.1. Terrestrial Impacts
                               5.4.2. Aquatic Impacts
                               6. Environmental Measurement and Monitoring Programs
                               6.1. Air Monitoring
                               6.2. Water Monitoring
                               6.3. Ecological Monitoring
                               6.4. Program Management
                               6.4.1. Data Collection
                               6.4.2. Data Analysis
                               7. Environmental Impacts of Postulated Accidents Involving Radioactive Materials
                               7.1. Accident Scenarios
                               7.2. Radiological Consequences
                               7.3. Mitigation Measures
                               7.4. Health Risks
                               8. Need for Power
                               8.1. Regional Energy Demand
                               8.2. Alternative Energy Sources
                               8.3. Future Projections
                               8.4. Justification for the Proposed Project
                               9. Alternatives to the Proposed Action
                               9.1. No-Action Alternative
                               9.2. Alternative Sites
                               9.3. Alternative Technologies
                               9.4. Comparison of Alternatives
                               10. Non-Radiological Health Impacts
                               10.1. Occupational Health
                               10.2. Public Health
                               10.3. Noise
                               10.4. Air Emissions
                               11. Radiological Health Impacts
                               11.1. Worker Exposure
                               11.2. Public Exposure
                               11.3. Dose Assessment
                               11.4. Radiation Protection
                               12. Cumulative Impacts
                               12.1. Past Actions
                               12.2. Present Actions
                               12.3. Future Actions
                               12.4. Combined Impacts
                               13. Mitigation Measures
                               13.1. Design Features
                               13.2. Operational Controls
                               13.3. Monitoring Programs
                               13.4. Contingency Plans
                               14. Conclusions
                               14.1. Summary of Findings
                               14.2. Environmental Significance
                               14.3. Effectiveness of Mitigation
                               14.4. Recommendations
                               15. References
                               15.1. Scientific Studies
                               15.2. Regulatory Documents
                               15.3. Technical Reports
                               15.4. Other Sources
                               16. Appendices
                               16.1. Technical Data
                               16.2. Modeling Results
                               16.3. Supporting Documentation
                               16.4. Additional Information
                               """;
        #endregion

        const string containerName = "ingest-nrc-environmental-review"; // Default value
        const string autoImportFolderName = "ingest-auto"; // Default value

        var indexNames = new[] { "index-us-nrc-envrep-sections" };

        // Create the document process definition
        
        var documentProcess = new DynamicDocumentProcessDefinition
        {
            Id = _nrcEnvironmentalReportId,
            ShortName = documentProcessShortName,
            Description = "NRC Environmental Review",
            BlobStorageContainerName = containerName,
            BlobStorageAutoImportFolderName = autoImportFolderName,
            LogicType = DocumentProcessLogicType.KernelMemory,
            Status = DocumentProcessStatus.Created,
            CompletionServiceType = DocumentProcessCompletionServiceType.GenericAiCompletionService,
            ClassifyDocuments = false,
            PrecedingSearchPartitionInclusionCount = 1,
            FollowingSearchPartitionInclusionCount = 1,
            NumberOfCitationsToGetFromRepository = 10,
            MinimumRelevanceForCitations = 0.7,
            DocumentOutline = documentOutline,
            Repositories = [.. indexNames],
            AiModelDeploymentId = _gpt4OModelDeploymentId,
            AiModelDeploymentForValidationId = _gpt4OModelDeploymentId
        };

        // Associate the outline with the document process
        documentOutline.DocumentProcessDefinitionId = _nrcEnvironmentalReportId;

        // Add the document process to the database
        dbContext.DynamicDocumentProcessDefinitions.Add(documentProcess);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Create a default sequential validation pipeline
        var pipelineId = Guid.NewGuid();
        var pipeline = new DocumentProcessValidationPipeline
        {
            Id = pipelineId,
            DocumentProcessId = _nrcEnvironmentalReportId,
            RunValidationAutomatically = false,
            ValidationPipelineSteps =
            [
                new DocumentProcessValidationPipelineStep
                {
                    Id = Guid.NewGuid(),
                    DocumentProcessValidationPipelineId = pipelineId,
                    PipelineExecutionType = ValidationPipelineExecutionType.SequentialFullDocument,
                    Order = 0
                }
            ]
        };

        // Associate the validation pipeline with the document process
        documentProcess.ValidationPipelineId = pipelineId;

        dbContext.DocumentProcessValidationPipelines.Add(pipeline);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Create prompt implementations directly
        await CreatePromptImplementationsForEnvironmentalReport(dbContext, _nrcEnvironmentalReportId, cancellationToken);

        // Create metadata fields to match the current form fields
        await CreateMetadataFieldsForEnvironmentalReport(dbContext, _nrcEnvironmentalReportId, cancellationToken);

        // Update the document process status to active
        documentProcess.Status = DocumentProcessStatus.Active;

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Environmental Report document process seeded successfully");
    }

    private async Task CreateMetadataFieldsForEnvironmentalReport(DocGenerationDbContext dbContext, Guid documentProcessId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating metadata fields for Environmental Report document process");

        var metadataFields = new List<DynamicDocumentProcessMetaDataField>
    {
        // Plant Details Section
        new()
        {
            Id = Guid.NewGuid(),
            DynamicDocumentProcessDefinitionId = documentProcessId,
            Name = "PlantName",
            DisplayName = "Plant Name",
            DescriptionToolTip = "Enter the full name of your plant (e.g., ABC Power Plant)",
            FieldType = DynamicDocumentProcessMetaDataFieldType.Text,
            IsRequired = true,
            Order = 0
        },
        new()
        {
            Id = Guid.NewGuid(),
            DynamicDocumentProcessDefinitionId = documentProcessId,
            Name = "Location",
            DisplayName = "Location",
            DescriptionToolTip = "Select the location of your plant on the map",
            FieldType = DynamicDocumentProcessMetaDataFieldType.MapComponent,
            IsRequired = true,
            Order = 1
        },
        new()
        {
            Id = Guid.NewGuid(),
            DynamicDocumentProcessDefinitionId = documentProcessId,
            Name = "PlantDesign",
            DisplayName = "Plant Design",
            DescriptionToolTip = "Select the design type(s) of your plant",
            FieldType = DynamicDocumentProcessMetaDataFieldType.MultiSelectWithPossibleValues,
            IsRequired = false,
            Order = 2,
            HasPossibleValues = true,
            PossibleValues =
            [
                "Light-Water Reactor (LWR)",
                "Pressurized Water Reactor (PWR)",
                "Boiling Water Reactor (BWR)"
            ],
        },
        new()
        {
            Id = Guid.NewGuid(),
            DynamicDocumentProcessDefinitionId = documentProcessId,
            Name = "OperatingHistory",
            DisplayName = "Operating History",
            DescriptionToolTip = "Provide a brief history of your plant's operation, including operational dates, outages, and any significant performance data",
            FieldType = DynamicDocumentProcessMetaDataFieldType.MultilineText,
            IsRequired = false,
            Order = 3
        },
        
        // Proposed Changes Section
        new()
        {
            Id = Guid.NewGuid(),
            DynamicDocumentProcessDefinitionId = documentProcessId,
            Name = "ReactorModel",
            DisplayName = "Reactor Model",
            DescriptionToolTip = "Specify the model of the reactor",
            FieldType = DynamicDocumentProcessMetaDataFieldType.Text,
            IsRequired = false,
            Order = 4
        },
        new()
        {
            Id = Guid.NewGuid(),
            DynamicDocumentProcessDefinitionId = documentProcessId,
            Name = "ProjectedProjectStartDate",
            DisplayName = "Projected Project Start Date",
            DescriptionToolTip = "The date when the project is expected to start",
            FieldType = DynamicDocumentProcessMetaDataFieldType.Date,
            IsRequired = false,
            Order = 5
        },
        new()
        {
            Id = Guid.NewGuid(),
            DynamicDocumentProcessDefinitionId = documentProcessId,
            Name = "ProjectedProjectEndDate",
            DisplayName = "Projected Project End Date",
            DescriptionToolTip = "The date when the project is expected to be completed",
            FieldType = DynamicDocumentProcessMetaDataFieldType.Date,
            IsRequired = false,
            Order = 6
        },
        new()
        {
            Id = Guid.NewGuid(),
            DynamicDocumentProcessDefinitionId = documentProcessId,
            Name = "ModificationDescription",
            DisplayName = "Modification Description",
            DescriptionToolTip = "Describe the proposed changes in detail. Include the purpose, scope, and impact on safety, security, and environmental aspects",
            FieldType = DynamicDocumentProcessMetaDataFieldType.MultilineText,
            IsRequired = false,
            Order = 7
        },
        
        // Licensee Information Section
        new()
        {
            Id = Guid.NewGuid(),
            DynamicDocumentProcessDefinitionId = documentProcessId,
            Name = "ApplicantName",
            DisplayName = "Name",
            DescriptionToolTip = "Contact name for the licensee",
            FieldType = DynamicDocumentProcessMetaDataFieldType.Text,
            IsRequired = false,
            Order = 8
        },
        new()
        {
            Id = Guid.NewGuid(),
            DynamicDocumentProcessDefinitionId = documentProcessId,
            Name = "OrganizationalStructure",
            DisplayName = "Organizational Structure",
            DescriptionToolTip = "Describe the organizational structure of your company or organization. Include roles and responsibilities related to nuclear operations",
            FieldType = DynamicDocumentProcessMetaDataFieldType.MultilineText,
            IsRequired = false,
            Order = 9
        },
        new()
        {
            Id = Guid.NewGuid(),
            DynamicDocumentProcessDefinitionId = documentProcessId,
            Name = "FinancialAssurance",
            DisplayName = "Financial Assurance",
            DescriptionToolTip = "Select the type of financial assurance you have in place",
            FieldType = DynamicDocumentProcessMetaDataFieldType.SelectDropdown,
            IsRequired = false,
            Order = 10,
            HasPossibleValues = true,
            PossibleValues =
            [
                "Self-assurance",
                "Third-party assurance",
            ]
        },
        new()
        {
            Id = Guid.NewGuid(),
            DynamicDocumentProcessDefinitionId = documentProcessId,
            Name = "ExperienceAndQualifications",
            DisplayName = "Experience and Qualifications",
            DescriptionToolTip = "Describe the relevant expertise and qualifications of key personnel involved in nuclear operations",
            FieldType = DynamicDocumentProcessMetaDataFieldType.MultilineText,
            IsRequired = false,
            Order = 11
        }
    };

        await dbContext.DynamicDocumentProcessMetaDataFields.AddRangeAsync(metadataFields, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created {Count} metadata fields for Environmental Report document process", metadataFields.Count);
    }

    private async Task CreatePromptImplementationsForEnvironmentalReport(DocGenerationDbContext dbContext, Guid documentProcessId, CancellationToken cancellationToken)
    {
        var documentProcess = await dbContext.DynamicDocumentProcessDefinitions
            .FindAsync([documentProcessId], cancellationToken);

        if (documentProcess == null)
        {
            _logger.LogWarning(
                "Cannot create prompt implementations: Document Process with Id {DocumentProcessId} not found",
                documentProcessId);
            return;
        }

        // Create a DefaultPromptCatalogTypes instance to get default prompt texts
        var defaultPromptCatalogTypes = new DefaultPromptCatalogTypes();

        // Get all prompt definitions from the database
        var promptDefinitions = await dbContext.PromptDefinitions.ToListAsync(cancellationToken);

        // Count of created prompt implementations
        int numberOfPromptImplementationsAdded = 0;

        // Loop through all properties in DefaultPromptCatalogTypes to create implementations
        foreach (var promptCatalogProperty in defaultPromptCatalogTypes.GetType()
                                                                 .GetProperties()
                                                                 .Where(p => p.PropertyType == typeof(string)))
        {
            // Find the corresponding prompt definition
            var promptDefinition = promptDefinitions.FirstOrDefault(pd => pd.ShortCode == promptCatalogProperty.Name);

            if (promptDefinition == null)
            {
                continue;
            }

            // Create a new prompt implementation
            var promptImplementation = new PromptImplementation
            {
                Id = Guid.NewGuid(),
                DocumentProcessDefinitionId = documentProcessId,
                PromptDefinitionId = promptDefinition.Id,
                Text = promptCatalogProperty.GetValue(defaultPromptCatalogTypes)?.ToString() ?? string.Empty
            };

            _logger.LogInformation(
                "Creating prompt implementation of prompt {PromptName} for DP {DocumentProcessShortname}",
                promptDefinition.ShortCode,
                documentProcess.ShortName);

            dbContext.PromptImplementations.Add(promptImplementation);
            numberOfPromptImplementationsAdded++;
        }

        if (numberOfPromptImplementationsAdded > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Created {NumberOfPromptImplementationsAdded} prompt implementations for DP {DocumentProcessShortname}",
                numberOfPromptImplementationsAdded,
                documentProcess.ShortName);
        }
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

    private async Task Seed2025_03_18_DefaultConfiguration(DocGenerationDbContext dbContext,
     CancellationToken cancellationToken)
    {
        // Check if the default configuration record already exists
        var configExists = await dbContext.Configurations.AnyAsync(
            c => c.Id == DbConfiguration.DefaultId, cancellationToken);

        // Default Configuration Values
        var defaultConfigurationItems = new Dictionary<string, string>
        {
            ["ServiceConfiguration:GreenlightServices:FrontEnd:SiteName"] = "Generative AI for Permitting",
            ["ServiceConfiguration:GreenlightServices:Scalability:NumberOfGenerationWorkers"] = "6",
            ["ServiceConfiguration:GreenlightServices:Scalability:NumberOfIngestionWorkers"] = "4",
            ["ServiceConfiguration:GreenlightServices:Scalability:NumberOfValidationWorkers"] = "4",
            ["ServiceConfiguration:GreenlightServices:Scalability:NumberOfReviewWorkers"] = "4"
        };

        // These items, if present, will always be removed as they shouldn't be managed by the database
        // configuration provider.

        var removeItems = new List<string>
        {
            "ServiceConfiguration:GreenlightServices:Scalability:UseAzureSignalR"
        };
        
        if (!configExists)
        {
            _logger.LogInformation("Creating default configuration record");

            // Create the default configuration with the initial values
            var defaultConfig = new DbConfiguration
            {
                Id = DbConfiguration.DefaultId,
                ConfigurationValues = JsonSerializer.Serialize(defaultConfigurationItems),
                LastUpdated = DateTime.UtcNow,
                LastUpdatedBy = "System"
            };

            dbContext.Configurations.Add(defaultConfig);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Default configuration record created successfully");
        }
        else
        {
            var config = await dbContext.Configurations.FirstAsync(
                c => c.Id == DbConfiguration.DefaultId, cancellationToken);
            
            var configurationItems = JsonSerializer.Deserialize<Dictionary<string, string>>(
                config.ConfigurationValues,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Dictionary<string, string>();

            bool updated = false;
            // Check if each key from configValues exists in the existing configuration
            foreach (var defaultConfigurationItem in defaultConfigurationItems)
            {
                if (!configurationItems.ContainsKey(defaultConfigurationItem.Key))
                {
                    configurationItems[defaultConfigurationItem.Key] = defaultConfigurationItem.Value;
                    updated = true;
                }
            }

            //Remove items that should not be managed by the database configuration provider
            foreach (var removeItem in removeItems)
            {
                if (configurationItems.Remove(removeItem))
                {
                    updated = true;
                }
            }

            if (updated)
            {
                config.ConfigurationValues = JsonSerializer.Serialize(configurationItems);
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
