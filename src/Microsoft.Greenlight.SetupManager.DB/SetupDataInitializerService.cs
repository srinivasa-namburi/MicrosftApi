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
using Npgsql;
using Microsoft.Extensions.Configuration;

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

        // Create kmvectordb and add pg_vector extension if needed
        await CreateKmVectorDbAndPgVectorExtensionAsync(scope.ServiceProvider, cancellationToken);

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

    private async Task CreateKmVectorDbAndPgVectorExtensionAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("kmvectordb");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("No connection string found for 'kmvectordb'. Skipping vector DB setup.");
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var targetDb = builder.Database;
        // Connect to the server's default DB (postgres or template1)
        builder.Database = "postgres";
        var adminConnectionString = builder.ToString();

        // Wait for server to be available (up to 30 seconds)
        var serverReady = false;
        var sw = Stopwatch.StartNew();
        Exception? lastException = null;
        while (!serverReady && sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            try
            {
                using var conn = new NpgsqlConnection(adminConnectionString);
                await conn.OpenAsync(cancellationToken);
                serverReady = true;
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(1000, cancellationToken);
            }
        }
        if (!serverReady)
        {
            _logger.LogError(lastException, "Could not connect to Postgres server for kmvectordb setup after 30 seconds.");
            return;
        }

        // Create database if it doesn't exist
        try
        {
            using var conn = new NpgsqlConnection(adminConnectionString);
            await conn.OpenAsync(cancellationToken);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT 1 FROM pg_database WHERE datname = @dbname;";
            cmd.Parameters.AddWithValue("@dbname", targetDb);
            var exists = await cmd.ExecuteScalarAsync(cancellationToken);
            if (exists == null)
            {
                _logger.LogInformation("Creating database '{DbName}'...", targetDb);
                using var createCmd = conn.CreateCommand();
                createCmd.CommandText = $"CREATE DATABASE \"{targetDb}\";";
                await createCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation("Database '{DbName}' already exists.", targetDb);
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring kmvectordb exists.");
            return;
        }

        // Add pg_vector extension if not present and create km schema if not present
        try
        {
            builder.Database = targetDb;
            var vectorDbConnectionString = builder.ToString();
            using var conn = new NpgsqlConnection(vectorDbConnectionString);
            await conn.OpenAsync(cancellationToken);
            using var cmd = conn.CreateCommand();
            // Ensure pgvector extension
            cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("pgvector extension ensured for database '{DbName}'.", targetDb);
            // Ensure km schema
            cmd.CommandText = "CREATE SCHEMA IF NOT EXISTS km;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Schema 'km' ensured for database '{DbName}'.", targetDb);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring pgvector extension or km schema for kmvectordb.");
        }
    }

    private async Task SeedAsync(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        using var activity =
            _activitySource.StartActivity("Seeding Document Generation Database", ActivityKind.Client);
        _logger.LogInformation("Seeding Document Generation Database started");
        var sw = Stopwatch.StartNew();

        var scope = _sp.CreateScope();
        // Ensure prompt definitions are up-to-date
        var promptDefinitionService = scope.ServiceProvider.GetRequiredService<IPromptDefinitionService>();
        await promptDefinitionService.EnsurePromptDefinitionsAsync(cancellationToken);

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

        // Create the document outline
        var documentOutline = new DocumentOutline();
        documentOutline.OutlineItems = new List<DocumentOutlineItem>();

        // Create the outline items programmatically
        // Level 0 items (top-level sections)
        var section1 = CreateOutlineItem("1", "Introduction", 0);
        var section2 = CreateOutlineItem("2", "Environmental Description", 0);
        var section3 = CreateOutlineItem("3", "Plant Description", 0);
        var section4 = CreateOutlineItem("4", "Environmental Impacts of Construction", 0);
        var section5 = CreateOutlineItem("5", "Environmental Impacts of Station Operation", 0);
        var section6 = CreateOutlineItem("6", "Environmental Measurement and Monitoring Programs", 0);
        var section7 = CreateOutlineItem("7", "Environmental Impacts of Postulated Accidents Involving Radioactive Materials", 0);
        var section8 = CreateOutlineItem("8", "Need for Power", 0);
        var section9 = CreateOutlineItem("9", "Alternatives to the Proposed Action", 0);
        var section10 = CreateOutlineItem("10", "Non-Radiological Health Impacts", 0);
        var section11 = CreateOutlineItem("11", "Radiological Health Impacts", 0);
        var section12 = CreateOutlineItem("12", "Cumulative Impacts", 0);
        var section13 = CreateOutlineItem("13", "Mitigation Measures", 0);
        var section14 = CreateOutlineItem("14", "Conclusions", 0);
        var section15 = CreateOutlineItem("15", "References", 0);
        var section16 = CreateOutlineItem("16", "Appendices", 0);

        section1.RenderTitleOnly = true;
        section2.RenderTitleOnly = true;
        section3.RenderTitleOnly = true;
        section4.RenderTitleOnly = true;
        section5.RenderTitleOnly = true;
        section6.RenderTitleOnly = true;
        section7.RenderTitleOnly = true;
        section8.RenderTitleOnly = true;
        section9.RenderTitleOnly = true;
        section10.RenderTitleOnly = true;
        section11.RenderTitleOnly = true;
        section12.RenderTitleOnly = true;
        section13.RenderTitleOnly = true;
        section14.RenderTitleOnly = true;
        section15.RenderTitleOnly = true;
        section16.RenderTitleOnly = true;

        // Add top-level items to the outline
        documentOutline.OutlineItems.Add(section1);
        documentOutline.OutlineItems.Add(section2);
        documentOutline.OutlineItems.Add(section3);
        documentOutline.OutlineItems.Add(section4);
        documentOutline.OutlineItems.Add(section5);
        documentOutline.OutlineItems.Add(section6);
        documentOutline.OutlineItems.Add(section7);
        documentOutline.OutlineItems.Add(section8);
        documentOutline.OutlineItems.Add(section9);
        documentOutline.OutlineItems.Add(section10);
        documentOutline.OutlineItems.Add(section11);
        documentOutline.OutlineItems.Add(section12);
        documentOutline.OutlineItems.Add(section13);
        documentOutline.OutlineItems.Add(section14);
        documentOutline.OutlineItems.Add(section15);
        documentOutline.OutlineItems.Add(section16);

        // Level 1 items for section 1
        var section1_1 = CreateOutlineItem("1.1", "Project Overview", 1, section1);
        var section1_2 = CreateOutlineItem("1.2", "Applicant Information", 1, section1);
        var section1_3 = CreateOutlineItem("1.3", "Site Location", 1, section1);
        var section1_4 = CreateOutlineItem("1.4", "Regulatory Requirements", 1, section1);

        // Level 1 items for section 2
        var section2_1 = CreateOutlineItem("2.1", "Land Use and Geology", 1, section2);
        var section2_2 = CreateOutlineItem("2.2", "Water Resources", 1, section2);
        var section2_3 = CreateOutlineItem("2.3", "Ecology", 1, section2);
        var section2_4 = CreateOutlineItem("2.4", "Climate and Meteorology", 1, section2);

        // Level 2 items for section 2.1
        var section2_1_1 = CreateOutlineItem("2.1.1", "Topography", 2, section2_1);
        var section2_1_2 = CreateOutlineItem("2.1.2", "Soil Characteristics", 2, section2_1);
        var section2_1_3 = CreateOutlineItem("2.1.3", "Seismic Conditions", 2, section2_1);

        // Level 2 items for section 2.2
        var section2_2_1 = CreateOutlineItem("2.2.1", "Surface Water", 2, section2_2);
        var section2_2_2 = CreateOutlineItem("2.2.2", "Groundwater", 2, section2_2);

        // Level 2 items for section 2.3
        var section2_3_1 = CreateOutlineItem("2.3.1", "Terrestrial Ecology", 2, section2_3);
        var section2_3_2 = CreateOutlineItem("2.3.2", "Aquatic Ecology", 2, section2_3);

        // Level 2 items for section 2.4
        var section2_4_1 = CreateOutlineItem("2.4.1", "Local Climate", 2, section2_4);
        var section2_4_2 = CreateOutlineItem("2.4.2", "Meteorological Data", 2, section2_4);

        // Level 1 items for section 3
        var section3_1 = CreateOutlineItem("3.1", "Plant Layout", 1, section3);
        var section3_2 = CreateOutlineItem("3.2", "Reactor Design", 1, section3);
        var section3_3 = CreateOutlineItem("3.3", "Auxiliary Systems", 1, section3);

        // Level 2 items for section 3.2
        var section3_2_1 = CreateOutlineItem("3.2.1", "Reactor Core", 2, section3_2);
        var section3_2_2 = CreateOutlineItem("3.2.2", "Safety Systems", 2, section3_2);

        // Level 2 items for section 3.3
        var section3_3_1 = CreateOutlineItem("3.3.1", "Cooling Systems", 2, section3_3);
        var section3_3_2 = CreateOutlineItem("3.3.2", "Waste Management Systems", 2, section3_3);

        // Level 1 items for section 4
        var section4_1 = CreateOutlineItem("4.1", "Land Disturbance", 1, section4);
        var section4_2 = CreateOutlineItem("4.2", "Air Quality", 1, section4);
        var section4_3 = CreateOutlineItem("4.3", "Water Quality", 1, section4);
        var section4_4 = CreateOutlineItem("4.4", "Noise Levels", 1, section4);
        var section4_5 = CreateOutlineItem("4.5", "Waste Generation", 1, section4);

        // Level 2 items for section 4.2
        var section4_2_1 = CreateOutlineItem("4.2.1", "Dust Generation", 2, section4_2);
        var section4_2_2 = CreateOutlineItem("4.2.2", "Emissions from Equipment", 2, section4_2);

        // Level 2 items for section 4.5
        var section4_5_1 = CreateOutlineItem("4.5.1", "Solid Waste", 2, section4_5);
        var section4_5_2 = CreateOutlineItem("4.5.2", "Hazardous Waste", 2, section4_5);

        // Level 1 items for section 5
        var section5_1 = CreateOutlineItem("5.1", "Air Quality", 1, section5);
        var section5_2 = CreateOutlineItem("5.2", "Water Quality", 1, section5);
        var section5_3 = CreateOutlineItem("5.3", "Land Use", 1, section5);
        var section5_4 = CreateOutlineItem("5.4", "Ecology", 1, section5);

        // Level 2 items for section 5.1
        var section5_1_1 = CreateOutlineItem("5.1.1", "Routine Emissions", 2, section5_1);
        var section5_1_2 = CreateOutlineItem("5.1.2", "Accidental Releases", 2, section5_1);

        // Level 2 items for section 5.2
        var section5_2_1 = CreateOutlineItem("5.2.1", "Thermal Discharge", 2, section5_2);
        var section5_2_2 = CreateOutlineItem("5.2.2", "Chemical Discharge", 2, section5_2);

        // Level 2 items for section 5.4
        var section5_4_1 = CreateOutlineItem("5.4.1", "Terrestrial Impacts", 2, section5_4);
        var section5_4_2 = CreateOutlineItem("5.4.2", "Aquatic Impacts", 2, section5_4);

        // Level 1 items for section 6
        var section6_1 = CreateOutlineItem("6.1", "Air Monitoring", 1, section6);
        var section6_2 = CreateOutlineItem("6.2", "Water Monitoring", 1, section6);
        var section6_3 = CreateOutlineItem("6.3", "Ecological Monitoring", 1, section6);
        var section6_4 = CreateOutlineItem("6.4", "Program Management", 1, section6);

        // Level 2 items for section 6.4
        var section6_4_1 = CreateOutlineItem("6.4.1", "Data Collection", 2, section6_4);
        var section6_4_2 = CreateOutlineItem("6.4.2", "Data Analysis", 2, section6_4);

        // Level 1 items for section 7
        var section7_1 = CreateOutlineItem("7.1", "Accident Scenarios", 1, section7);
        var section7_2 = CreateOutlineItem("7.2", "Radiological Consequences", 1, section7);
        var section7_3 = CreateOutlineItem("7.3", "Mitigation Measures", 1, section7);
        var section7_4 = CreateOutlineItem("7.4", "Health Risks", 1, section7);

        // Level 1 items for section 8
        var section8_1 = CreateOutlineItem("8.1", "Regional Energy Demand", 1, section8);
        var section8_2 = CreateOutlineItem("8.2", "Alternative Energy Sources", 1, section8);
        var section8_3 = CreateOutlineItem("8.3", "Future Projections", 1, section8);
        var section8_4 = CreateOutlineItem("8.4", "Justification for the Proposed Project", 1, section8);

        // Level 1 items for section 9
        var section9_1 = CreateOutlineItem("9.1", "No-Action Alternative", 1, section9);
        var section9_2 = CreateOutlineItem("9.2", "Alternative Sites", 1, section9);
        var section9_3 = CreateOutlineItem("9.3", "Alternative Technologies", 1, section9);
        var section9_4 = CreateOutlineItem("9.4", "Comparison of Alternatives", 1, section9);

        // Level 1 items for section 10
        var section10_1 = CreateOutlineItem("10.1", "Occupational Health", 1, section10);
        var section10_2 = CreateOutlineItem("10.2", "Public Health", 1, section10);
        var section10_3 = CreateOutlineItem("10.3", "Noise", 1, section10);
        var section10_4 = CreateOutlineItem("10.4", "Air Emissions", 1, section10);

        // Level 1 items for section 11
        var section11_1 = CreateOutlineItem("11.1", "Worker Exposure", 1, section11);
        var section11_2 = CreateOutlineItem("11.2", "Public Exposure", 1, section11);
        var section11_3 = CreateOutlineItem("11.3", "Dose Assessment", 1, section11);
        var section11_4 = CreateOutlineItem("11.4", "Radiation Protection", 1, section11);

        // Level 1 items for section 12
        var section12_1 = CreateOutlineItem("12.1", "Past Actions", 1, section12);
        var section12_2 = CreateOutlineItem("12.2", "Present Actions", 1, section12);
        var section12_3 = CreateOutlineItem("12.3", "Future Actions", 1, section12);
        var section12_4 = CreateOutlineItem("12.4", "Combined Impacts", 1, section12);

        // Level 1 items for section 13
        var section13_1 = CreateOutlineItem("13.1", "Design Features", 1, section13);
        var section13_2 = CreateOutlineItem("13.2", "Operational Controls", 1, section13);
        var section13_3 = CreateOutlineItem("13.3", "Monitoring Programs", 1, section13);
        var section13_4 = CreateOutlineItem("13.4", "Contingency Plans", 1, section13);

        // Level 1 items for section 14
        var section14_1 = CreateOutlineItem("14.1", "Summary of Findings", 1, section14);
        var section14_2 = CreateOutlineItem("14.2", "Environmental Significance", 1, section14);
        var section14_3 = CreateOutlineItem("14.3", "Effectiveness of Mitigation", 1, section14);
        var section14_4 = CreateOutlineItem("14.4", "Recommendations", 1, section14);

        // Level 1 items for section 15
        var section15_1 = CreateOutlineItem("15.1", "Scientific Studies", 1, section15);
        var section15_2 = CreateOutlineItem("15.2", "Regulatory Documents", 1, section15);
        var section15_3 = CreateOutlineItem("15.3", "Technical Reports", 1, section15);
        var section15_4 = CreateOutlineItem("15.4", "Other Sources", 1, section15);

        // Level 1 items for section 16
        var section16_1 = CreateOutlineItem("16.1", "Technical Data", 1, section16);
        var section16_2 = CreateOutlineItem("16.2", "Modeling Results", 1, section16);
        var section16_3 = CreateOutlineItem("16.3", "Supporting Documentation", 1, section16);
        var section16_4 = CreateOutlineItem("16.4", "Additional Information", 1, section16);

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
            LogicType = DocumentProcessLogicType.SemanticKernelVectorStore,
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

        //// Create a default sequential validation pipeline
        //var pipelineId = Guid.NewGuid();
        //var pipeline = new DocumentProcessValidationPipeline
        //{
        //    Id = pipelineId,
        //    DocumentProcessId = _nrcEnvironmentalReportId,
        //    RunValidationAutomatically = false,
        //    ValidationPipelineSteps =
        //    [
        //        new DocumentProcessValidationPipelineStep
        //    {
        //        Id = Guid.NewGuid(),
        //        DocumentProcessValidationPipelineId = pipelineId,
        //        PipelineExecutionType = ValidationPipelineExecutionType.SequentialFullDocument,
        //        Order = 0
        //    }
        //    ]
        //};

        //// Associate the validation pipeline with the document process
        //documentProcess.ValidationPipelineId = pipelineId;

        //dbContext.DocumentProcessValidationPipelines.Add(pipeline);

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

    /// <summary>
    /// Creates a document outline item with the specified parameters and appropriate order
    /// </summary>
    private DocumentOutlineItem CreateOutlineItem(string sectionNumber, string sectionTitle, int level, DocumentOutlineItem? parent = null)
    {
        // Set order based on section number
        int orderIndex = 0;

        if (!string.IsNullOrEmpty(sectionNumber))
        {
            // Parse the section number for ordering
            var parts = sectionNumber.Split('.');
            if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int lastNumber))
            {
                orderIndex = lastNumber;
            }
        }

        var item = new DocumentOutlineItem
        {
            Id = Guid.NewGuid(),
            SectionNumber = sectionNumber,
            SectionTitle = sectionTitle,
            Level = level,
            OrderIndex = orderIndex,
            Children = new List<DocumentOutlineItem>()
        };

        if (parent != null)
        {
            item.ParentId = parent.Id;
            parent.Children.Add(item);
        }

        return item;
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
            DisplayName = "Applicant Name",
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