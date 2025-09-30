// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.Components;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.Configuration;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Models.Validation;
using Microsoft.Greenlight.Shared.Services;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Npgsql;
using Microsoft.Greenlight.Shared.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.Greenlight.Shared.Helpers;

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

    private readonly Guid _textEmbeddingAda002ModelId = Guid.Parse("e8b4f2d1-1c7e-4a3b-9f2d-6e5a8b9c0d1e", CultureInfo.InvariantCulture);
    private readonly Guid _textEmbeddingAda002ModelDeploymentId = Guid.Parse("a1b2c3d4-5e6f-7a8b-9c0d-1e2f3a4b5c6d", CultureInfo.InvariantCulture);

    private readonly Guid _nrcEnvironmentalReportId = Guid.Parse("88ffae0a-22a3-42e0-a538-72dd1a589216", CultureInfo.InvariantCulture);

    // Fixed IDs for content reference storage entities to ensure idempotency
    private readonly Guid _contentReferenceStorageSourceId = Guid.Parse("7f3e4b9a-2c5d-4e8f-9a1b-3c6d8e9f0a1b", CultureInfo.InvariantCulture);
    private readonly Guid _contentReferenceStorageCategoryId = Guid.Parse("8a4f5c0b-3d6e-5f9a-0b2c-4d7e9f0b2c3d", CultureInfo.InvariantCulture);

    // Fixed IDs for ContentReferenceType to FileStorageSource mappings
    private readonly Dictionary<ContentReferenceType, Guid> _contentReferenceTypeMappingIds = new()
    {
        [ContentReferenceType.GeneratedDocument] = Guid.Parse("9b5a6d1c-4e7f-6a0b-1c3d-5e8f0c3d4e5f", CultureInfo.InvariantCulture),
        [ContentReferenceType.GeneratedSection] = Guid.Parse("0c6b7e2d-5f8a-7b1c-2d4e-6f9a1d4e5f6a", CultureInfo.InvariantCulture),
        [ContentReferenceType.ReviewItem] = Guid.Parse("1d7c8f3e-6a9b-8c2d-3e5f-7a0b2e5f6a7b", CultureInfo.InvariantCulture),
        [ContentReferenceType.ExternalFile] = Guid.Parse("2e8d9a4f-7b0c-9d3e-4f6a-8b1c3f6a7b8c", CultureInfo.InvariantCulture),
        [ContentReferenceType.ExternalLinkAsset] = Guid.Parse("3f9e0b5a-8c1d-0e4f-5a7b-9c2d4a7b8c9d", CultureInfo.InvariantCulture)
    };


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

        // Configure SQL Server database users for workload identity and pipeline access
        await ConfigureSqlDatabaseUsersAsync(scope.ServiceProvider, cancellationToken);

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
        if (string.IsNullOrWhiteSpace(targetDb))
        {
            _logger.LogWarning("Connection string for 'kmvectordb' is missing the database name. Skipping vector DB setup.");
            return;
        }
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
                await using var conn = new NpgsqlConnection(adminConnectionString);
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
            await using var conn = new NpgsqlConnection(adminConnectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
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
            await using var conn = new NpgsqlConnection(vectorDbConnectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
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

    private async Task ConfigureSqlDatabaseUsersAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Configuring SQL Database Users", ActivityKind.Client);

        // Only run SQL user configuration in production environments
        if (!AdminHelper.IsRunningInProduction())
        {
            _logger.LogInformation("Skipping SQL user configuration - not running in production environment");
            return;
        }

        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("ProjectVicoDB");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            _logger.LogWarning("No connection string found for ProjectVicoDB. Skipping SQL user configuration.");
            return;
        }

        try
        {
            _logger.LogInformation("Attempting SQL Server connection with workload identity authentication...");
            _logger.LogInformation("Connection string: {ConnectionStringMasked}",
                string.Join(";", connectionString.Split(';').Where(p => !p.Contains("Password", StringComparison.OrdinalIgnoreCase))));

            // Log workload identity environment variables for diagnostics
            _logger.LogInformation("Workload identity environment - AZURE_CLIENT_ID: {ClientId}, AZURE_TENANT_ID: {TenantId}, AZURE_USE_WORKLOAD_IDENTITY: {UseWI}",
                Environment.GetEnvironmentVariable("AZURE_CLIENT_ID"),
                Environment.GetEnvironmentVariable("AZURE_TENANT_ID"),
                Environment.GetEnvironmentVariable("AZURE_USE_WORKLOAD_IDENTITY"));

            await using var sqlConnection = new SqlConnection(connectionString);

            // Set up info message handler before opening connection
            sqlConnection.InfoMessage += (sender, e) => _logger.LogInformation("SQL: {Message}", e.Message);

            await sqlConnection.OpenAsync(cancellationToken);

            _logger.LogInformation("✅ Connected to SQL Server successfully. Configuring database users...");

            // Get workload identity name from environment
            var wiIdentityName = Environment.GetEnvironmentVariable("WORKLOAD_IDENTITY_NAME") ?? "uami-aks-cluster";

            var sql = $@"
-- Ensure workload identity user exists
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = '{wiIdentityName}')
BEGIN
    CREATE USER [{wiIdentityName}] FROM EXTERNAL PROVIDER;
    PRINT 'Created workload identity user: {wiIdentityName}';
END
ELSE
    PRINT 'Workload identity user already exists: {wiIdentityName}';

-- Grant database owner permissions to workload identity
IF NOT IS_ROLEMEMBER('db_owner', '{wiIdentityName}') = 1
BEGIN
    ALTER ROLE db_owner ADD MEMBER [{wiIdentityName}];
    PRINT 'Granted db_owner role to workload identity: {wiIdentityName}';
END
ELSE
    PRINT 'Workload identity already has db_owner role: {wiIdentityName}';


PRINT 'SQL Server database user configuration completed successfully';
";

            await using var command = new SqlCommand(sql, sqlConnection);
            command.CommandTimeout = 30;

            // Use ExecuteNonQueryAsync instead of ExecuteReaderAsync for DDL commands
            await command.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("SQL Server database users configured successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not configure SQL Server database users automatically. This may require manual configuration.");
            _logger.LogInformation("To configure manually, connect to the database as an admin and run:");
            var identityName = Environment.GetEnvironmentVariable("WORKLOAD_IDENTITY_NAME") ?? "uami-aks-cluster";
            _logger.LogInformation("CREATE USER [{IdentityName}] FROM EXTERNAL PROVIDER; ALTER ROLE db_owner ADD MEMBER [{IdentityName}];", identityName, identityName);
        }

        activity?.Stop();
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

        // Seed Authorization (permissions, roles, and bootstrap assignments)
        // Runs here so it's guaranteed before the rest of the app cluster comes online.
        var authSeederLogger = scope.ServiceProvider.GetRequiredService<ILogger<AuthorizationSeeder>>();
        var authSeeder = new AuthorizationSeeder(dbContext, authSeederLogger);
        await authSeeder.EnsureSeededAsync(cancellationToken);

        await Seed2024_05_24_OrphanedChatMessagesCleanup(dbContext, cancellationToken);
        await Seed2025_02_27_CreateDefaultSequentialValidationPipeline(dbContext, cancellationToken);
        await Seed2025_03_18_DefaultConfiguration(dbContext, cancellationToken);
        await Seed2025_04_24_AiModelSettings(dbContext, cancellationToken);
        await Seed2025_04_24_DefaultAiModelDeploymentForDocumentProcesses(dbContext, cancellationToken);
        await Seed2025_12_15_EmbeddingModelSettings(dbContext, cancellationToken);
        await Seed2025_09_02_DefaultFileStorageSource(dbContext, cancellationToken);
        await Seed2025_09_02_MigrateFileStorageToHostArchitecture(dbContext, cancellationToken);
        await Seed2025_09_02_MigrateLegacyBlobStorageToFileStorageSources(dbContext, cancellationToken);
        await Seed2025_09_04_RebuildAllFileAcknowledgmentRecords(dbContext, cancellationToken);
        await Seed2025_09_08_BackfillFileStorageSourceDataType(dbContext, cancellationToken);
        await Seed2025_09_15_BackfillDisplayFileName(dbContext, cancellationToken);


        sw.Stop();
        _logger.LogInformation(
            "Seeding Document Generation Database completed in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);

        activity!.Stop();
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

    private async Task Seed2025_09_08_BackfillFileStorageSourceDataType(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        // Only run if there are existing sources with an invalid/legacy value (0) for StorageSourceDataType
        var zeroTypeCount = await dbContext.FileStorageSources
            .CountAsync(s => (int)s.StorageSourceDataType == 0, cancellationToken);

        if (zeroTypeCount == 0)
        {
            _logger.LogInformation("No FileStorageSources with legacy StorageSourceDataType=0 found. Skipping backfill.");
            return;
        }

        _logger.LogInformation("Backfilling StorageSourceDataType for {Count} FileStorageSources with legacy value 0", zeroTypeCount);

        // Identify sources that are the content-reference container or already mapped for content references
        var contentRefContainerName = "content-references";

        var contentRefMappedSourceIds = await dbContext.ContentReferenceTypeFileStorageSources
            .Select(m => m.FileStorageSourceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var contentRefSources = await dbContext.FileStorageSources
            .Include(s => s.Categories)
            .Where(s => (int)s.StorageSourceDataType == 0 &&
                        (s.ContainerOrPath == contentRefContainerName ||
                         s.Categories.Any(c => c.DataType == FileStorageSourceDataType.ContentReference) ||
                         contentRefMappedSourceIds.Contains(s.Id)))
            .ToListAsync(cancellationToken);

        foreach (var src in contentRefSources)
        {
            src.StorageSourceDataType = FileStorageSourceDataType.ContentReference;
        }

        // Identify sources tied to any DocumentProcess or DocumentLibrary and still at 0
        var ingestionLinkedSourceIds = await dbContext.DocumentProcessFileStorageSources
            .Select(dpfs => dpfs.FileStorageSourceId)
            .Union(dbContext.DocumentLibraryFileStorageSources.Select(dlfs => dlfs.FileStorageSourceId))
            .Distinct()
            .ToListAsync(cancellationToken);

        var ingestionSources = await dbContext.FileStorageSources
            .Where(s => (int)s.StorageSourceDataType == 0 && ingestionLinkedSourceIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

        foreach (var src in ingestionSources)
        {
            src.StorageSourceDataType = FileStorageSourceDataType.Ingestion;
        }

        // If some sources remain with legacy value 0 and were not matched by the above heuristics,
        // set them to a sensible default to avoid rerunning this migration on every startup.
        var remainingZeroSources = await dbContext.FileStorageSources
            .Where(s => (int)s.StorageSourceDataType == 0)
            .ToListAsync(cancellationToken);

        if (remainingZeroSources.Any())
        {
            foreach (var src in remainingZeroSources)
            {
                // Heuristic: if the container/path contains "content-reference" treat as ContentReference
                // otherwise default to Ingestion so sources are usable by ingestion flows.
                if (!string.IsNullOrEmpty(src.ContainerOrPath) &&
                    (src.ContainerOrPath.Contains("content-reference", StringComparison.OrdinalIgnoreCase) ||
                     src.ContainerOrPath.Contains("content-references", StringComparison.OrdinalIgnoreCase)))
                {
                    src.StorageSourceDataType = FileStorageSourceDataType.ContentReference;
                }
                else
                {
                    src.StorageSourceDataType = FileStorageSourceDataType.Ingestion;
                }
            }
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Backfill complete: updated {ContentRefCount} ContentReference and {IngestionCount} Ingestion sources (plus {RemainingCount} remaining defaults applied).",
                contentRefSources.Count, ingestionSources.Count, remainingZeroSources.Count);
        }
        else
        {
            _logger.LogInformation("Backfill made no changes (all sources already set).");
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

    private async Task Seed2025_12_15_EmbeddingModelSettings(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        // Ensure default embedding model exists
        var embeddingModel = await dbContext.AiModels.FindAsync([_textEmbeddingAda002ModelId], cancellationToken);
        if (embeddingModel == null)
        {
            embeddingModel = new AiModel
            {
                Id = _textEmbeddingAda002ModelId,
                Name = "text-embedding-ada-002",
                ModelType = AiModelType.Embedding,
                EmbeddingSettings = new Microsoft.Greenlight.Shared.Contracts.Components.AiModelEmbeddingSettings
                {
                    Dimensions = 1536,
                    MaxContentLength = 8192
                },
                IsReasoningModel = false
            };
            dbContext.AiModels.Add(embeddingModel);
        }

        // Ensure default embedding deployment exists
        var embeddingDeployment = await dbContext.AiModelDeployments.FindAsync([_textEmbeddingAda002ModelDeploymentId], cancellationToken);
        if (embeddingDeployment == null)
        {
            embeddingDeployment = new AiModelDeployment
            {
                Id = _textEmbeddingAda002ModelDeploymentId,
                DeploymentName = "text-embedding-ada-002",
                AiModelId = _textEmbeddingAda002ModelId,
                EmbeddingSettings = embeddingModel.EmbeddingSettings
            };
            dbContext.AiModelDeployments.Add(embeddingDeployment);
        }

        // Backfill ModelType for existing models that don't have it set (default to Chat)
        var modelsWithoutType = await dbContext.AiModels
            .Where(m => m.ModelType == 0 && m.Id != _textEmbeddingAda002ModelId)
            .ToListAsync(cancellationToken);
        foreach (var model in modelsWithoutType)
        {
            model.ModelType = AiModelType.Chat;
            dbContext.AiModels.Update(model);
        }

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
        {
            return;
        }

        _logger.LogInformation("Seeding : Creating default validation pipeline for {Count} DocumentProcesses", documentProcesses.Count);

        foreach (var dp in documentProcesses)
        {
            var pipeline = new DocumentProcessValidationPipeline
            {
                Id = Guid.NewGuid(),
                DocumentProcessId = dp.Id,
                ValidationPipelineSteps = new List<DocumentProcessValidationPipelineStep>
                {
                    new DocumentProcessValidationPipelineStep
                    {
                        Id = Guid.NewGuid(),
                        DocumentProcessValidationPipelineId = dp.Id,
                        PipelineExecutionType = ValidationPipelineExecutionType.SequentialFullDocument,
                        Order = 0
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

    private async Task Seed2025_09_02_DefaultFileStorageSource(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        var existingHostsCount = await dbContext.FileStorageHosts.CountAsync(cancellationToken);

        if (existingHostsCount > 0)
        {
            _logger.LogInformation("File storage hosts already exist ({Count} found). Skipping default file storage host/source creation.", existingHostsCount);

            var existingDefaultHost = await dbContext.FileStorageHosts
                .FirstOrDefaultAsync(h => h.IsDefault || h.ConnectionString == "default", cancellationToken);

            if (existingDefaultHost != null)
            {
                const string contentRefContainerExistingHost = "content-references";
                var existingContentRefSourceExistingHost = await dbContext.FileStorageSources
                    .FirstOrDefaultAsync(s => s.FileStorageHostId == existingDefaultHost.Id && s.ContainerOrPath == contentRefContainerExistingHost, cancellationToken);

                if (existingContentRefSourceExistingHost == null)
                {
                    var contentRefSource = new Microsoft.Greenlight.Shared.Models.FileStorage.FileStorageSource
                    {
                        Id = _contentReferenceStorageSourceId,
                        Name = "Content References",
                        FileStorageHostId = existingDefaultHost.Id,
                        ContainerOrPath = contentRefContainerExistingHost,
                        AutoImportFolderName = null,
                        IsDefault = false,
                        IsActive = true,
                        ShouldMoveFiles = false,
                        Description = "Container for ContentReference file uploads",
                        StorageSourceDataType = Microsoft.Greenlight.Shared.Enums.FileStorageSourceDataType.ContentReference
                    };

                    dbContext.FileStorageSources.Add(contentRefSource);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    // Check if category already exists
                    var existingCategory = await dbContext.FileStorageSourceCategories
                        .FirstOrDefaultAsync(c => c.Id == _contentReferenceStorageCategoryId, cancellationToken);

                    if (existingCategory == null)
                    {
                        dbContext.FileStorageSourceCategories.Add(new Microsoft.Greenlight.Shared.Models.FileStorage.FileStorageSourceCategory
                        {
                            Id = _contentReferenceStorageCategoryId,
                            FileStorageSourceId = contentRefSource.Id,
                            DataType = Microsoft.Greenlight.Shared.Enums.FileStorageSourceDataType.ContentReference
                        });
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }

                    foreach (var typeValue in Enum.GetValues(typeof(Microsoft.Greenlight.Shared.Enums.ContentReferenceType)).Cast<Microsoft.Greenlight.Shared.Enums.ContentReferenceType>())
                    {
                        // Check if mapping already exists before creating
                        var mappingId = _contentReferenceTypeMappingIds[typeValue];
                        var existingMapping = await dbContext.ContentReferenceTypeFileStorageSources
                            .FirstOrDefaultAsync(m => m.Id == mappingId, cancellationToken);

                        if (existingMapping == null)
                        {
                            dbContext.ContentReferenceTypeFileStorageSources.Add(new Microsoft.Greenlight.Shared.Models.FileStorage.ContentReferenceTypeFileStorageSource
                            {
                                Id = mappingId,
                                ContentReferenceType = typeValue,
                                FileStorageSourceId = contentRefSource.Id,
                                Priority = 0,
                                IsActive = true,
                                AcceptsUploads = typeValue == Microsoft.Greenlight.Shared.Enums.ContentReferenceType.ExternalLinkAsset
                            });
                        }
                    }
                    await dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("Created content reference storage source on existing host: {SourceName} (ID: {SourceId})", contentRefSource.Name, contentRefSource.Id);
                }
            }
            return;
        }

        _logger.LogInformation("Creating default file storage host and source for primary blob storage account");

        const string defaultHostName = "Default Blob Storage Host";
        var existingHost = await dbContext.FileStorageHosts
            .FirstOrDefaultAsync(h => h.Name == defaultHostName, cancellationToken);
        if (existingHost != null)
        {
            _logger.LogInformation("Default file storage host with name '{HostName}' already exists. Skipping creation.", defaultHostName);
            return;
        }

        var defaultFileStorageHost = new Microsoft.Greenlight.Shared.Models.FileStorage.FileStorageHost
        {
            Id = Guid.NewGuid(),
            Name = defaultHostName,
            ProviderType = Microsoft.Greenlight.Shared.Enums.FileStorageProviderType.BlobStorage,
            ConnectionString = "default",
            IsDefault = true,
            IsActive = true,
            AuthenticationKey = null,
            Description = "Default Azure Blob Storage host using the primary storage account"
        };
        dbContext.FileStorageHosts.Add(defaultFileStorageHost);
        await dbContext.SaveChangesAsync(cancellationToken);

        const string defaultContainerName = "default-container";
        var existingSource = await dbContext.FileStorageSources
            .FirstOrDefaultAsync(s => s.FileStorageHostId == defaultFileStorageHost.Id && s.ContainerOrPath == defaultContainerName, cancellationToken);
        if (existingSource != null)
        {
            _logger.LogInformation("Default file storage source for container '{Container}' already exists. Skipping creation.", defaultContainerName);
            return;
        }

        var defaultFileStorageSource = new Microsoft.Greenlight.Shared.Models.FileStorage.FileStorageSource
        {
            Id = Guid.NewGuid(),
            Name = "Default Blob Storage",
            FileStorageHostId = defaultFileStorageHost.Id,
            ContainerOrPath = defaultContainerName,
            AutoImportFolderName = "ingest-auto",
            IsDefault = true,
            IsActive = true,
            ShouldMoveFiles = true,
            Description = "Default container for blob storage operations",
            StorageSourceDataType = Microsoft.Greenlight.Shared.Enums.FileStorageSourceDataType.Ingestion
        };
        dbContext.FileStorageSources.Add(defaultFileStorageSource);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.FileStorageSourceCategories.Add(new Microsoft.Greenlight.Shared.Models.FileStorage.FileStorageSourceCategory
        {
            Id = Guid.NewGuid(),
            FileStorageSourceId = defaultFileStorageSource.Id,
            DataType = Microsoft.Greenlight.Shared.Enums.FileStorageSourceDataType.Ingestion
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created default file storage host: {HostName} (ID: {HostId})", defaultFileStorageHost.Name, defaultFileStorageHost.Id);
        _logger.LogInformation("Created default file storage source: {SourceName} (ID: {SourceId})", defaultFileStorageSource.Name, defaultFileStorageSource.Id);

        const string contentRefContainerNewHost = "content-references";
        var existingContentRefSourceNewHost = await dbContext.FileStorageSources
            .FirstOrDefaultAsync(s => s.FileStorageHostId == defaultFileStorageHost.Id && s.ContainerOrPath == contentRefContainerNewHost, cancellationToken);
        if (existingContentRefSourceNewHost == null)
        {
            var contentRefSource = new Microsoft.Greenlight.Shared.Models.FileStorage.FileStorageSource
            {
                Id = _contentReferenceStorageSourceId,
                Name = "Content References",
                FileStorageHostId = defaultFileStorageHost.Id,
                ContainerOrPath = contentRefContainerNewHost,
                AutoImportFolderName = null,
                IsDefault = false,
                IsActive = true,
                ShouldMoveFiles = false,
                Description = "Container for ContentReference file uploads",
                StorageSourceDataType = Microsoft.Greenlight.Shared.Enums.FileStorageSourceDataType.ContentReference
            };
            dbContext.FileStorageSources.Add(contentRefSource);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Check if category already exists
            var existingCategory = await dbContext.FileStorageSourceCategories
                .FirstOrDefaultAsync(c => c.Id == _contentReferenceStorageCategoryId, cancellationToken);

            if (existingCategory == null)
            {
                dbContext.FileStorageSourceCategories.Add(new Microsoft.Greenlight.Shared.Models.FileStorage.FileStorageSourceCategory
                {
                    Id = _contentReferenceStorageCategoryId,
                    FileStorageSourceId = contentRefSource.Id,
                    DataType = Microsoft.Greenlight.Shared.Enums.FileStorageSourceDataType.ContentReference
                });
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            foreach (var typeValue in Enum.GetValues(typeof(Microsoft.Greenlight.Shared.Enums.ContentReferenceType)).Cast<Microsoft.Greenlight.Shared.Enums.ContentReferenceType>())
            {
                // Check if mapping already exists before creating
                var mappingId = _contentReferenceTypeMappingIds[typeValue];
                var existingMapping = await dbContext.ContentReferenceTypeFileStorageSources
                    .FirstOrDefaultAsync(m => m.Id == mappingId, cancellationToken);

                if (existingMapping == null)
                {
                    dbContext.ContentReferenceTypeFileStorageSources.Add(new Microsoft.Greenlight.Shared.Models.FileStorage.ContentReferenceTypeFileStorageSource
                    {
                        Id = mappingId,
                        ContentReferenceType = typeValue,
                        FileStorageSourceId = contentRefSource.Id,
                        Priority = 0,
                        IsActive = true,
                        AcceptsUploads = typeValue == Microsoft.Greenlight.Shared.Enums.ContentReferenceType.ExternalFile
                    });
                }
            }
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Created content reference storage source: {SourceName} (ID: {SourceId})", contentRefSource.Name, contentRefSource.Id);
        }
    }

    private async Task Seed2025_09_02_MigrateFileStorageToHostArchitecture(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        // This method handles any remaining file storage sources that need host assignment after migration

        _logger.LogInformation("Checking for file storage sources that need host assignment after migration");

        // Check for any file storage sources that don't have a valid FileStorageHostId assigned
        // (excluding Guid.Empty which might have been set by migration)
        var sourcesWithoutHost = await dbContext.FileStorageSources
            .Where(s => s.FileStorageHostId == Guid.Empty)
            .ToListAsync(cancellationToken);

        if (!sourcesWithoutHost.Any())
        {
            _logger.LogInformation("All file storage sources have proper host assignments");
            return;
        }

        _logger.LogInformation("Found {Count} file storage sources without host assignments, assigning them to existing or new default host", sourcesWithoutHost.Count);

        // Get existing default host or create one
        var defaultHost = await dbContext.FileStorageHosts
            .FirstOrDefaultAsync(h => h.IsDefault, cancellationToken);

        if (defaultHost == null)
        {
            // Check if a host with our default name already exists but isn't marked as default
            const string defaultHostName = "Post-Migration Default Blob Storage Host";
            var existingHostByName = await dbContext.FileStorageHosts
                .FirstOrDefaultAsync(h => h.Name == defaultHostName, cancellationToken);

            if (existingHostByName != null)
            {
                // Use the existing host and mark it as default
                existingHostByName.IsDefault = true;
                defaultHost = existingHostByName;
                _logger.LogInformation("Found existing host '{HostName}', marking as default", defaultHostName);
            }
            else
            {
                // Create a default host if none exists (shouldn't happen if migration worked correctly)
                defaultHost = new Microsoft.Greenlight.Shared.Models.FileStorage.FileStorageHost
                {
                    Id = Guid.NewGuid(),
                    Name = defaultHostName,
                    ProviderType = Microsoft.Greenlight.Shared.Enums.FileStorageProviderType.BlobStorage,
                    ConnectionString = "default",
                    IsDefault = true,
                    IsActive = true,
                    AuthenticationKey = null,
                    Description = "Default host created during post-migration cleanup"
                };

                dbContext.FileStorageHosts.Add(defaultHost);
                _logger.LogInformation("Created post-migration default host: {HostName} (ID: {HostId})", defaultHost.Name, defaultHost.Id);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Assign all orphaned sources to the default host
        foreach (var source in sourcesWithoutHost)
        {
            source.FileStorageHostId = defaultHost.Id;
            _logger.LogInformation("Assigned source {SourceName} (ID: {SourceId}) to host {HostName}",
                source.Name, source.Id, defaultHost.Name);
        }

        if (sourcesWithoutHost.Any())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully assigned {Count} file storage sources to default host", sourcesWithoutHost.Count);
        }
    }

    private async Task Seed2025_09_02_MigrateLegacyBlobStorageToFileStorageSources(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting migration of legacy blob storage properties to FileStorageSource architecture");

        // Get the default host (should exist from previous migration)
        var defaultHost = await dbContext.FileStorageHosts
            .FirstOrDefaultAsync(h => h.IsDefault && h.IsActive, cancellationToken);

        if (defaultHost == null)
        {
            _logger.LogError("Default FileStorageHost not found. Cannot migrate legacy blob storage properties.");
            return;
        }

        // Migrate DocumentProcess entities
        var processesWithLegacyStorage = await dbContext.DynamicDocumentProcessDefinitions
            .Where(p => !string.IsNullOrEmpty(p.BlobStorageContainerName) &&
                       !p.FileStorageSources.Any())
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} document processes with legacy blob storage properties to migrate", processesWithLegacyStorage.Count);

        foreach (var process in processesWithLegacyStorage)
        {
            // Check if a FileStorageSource already exists for this container/host combination
            var existingSource = await dbContext.FileStorageSources
                .FirstOrDefaultAsync(s => s.FileStorageHostId == defaultHost.Id &&
                                         s.ContainerOrPath == process.BlobStorageContainerName, cancellationToken);

            Microsoft.Greenlight.Shared.Models.FileStorage.FileStorageSource fileStorageSource;

            if (existingSource != null)
            {
                // Use existing source
                fileStorageSource = existingSource;
                _logger.LogInformation("Using existing FileStorageSource for process: {ProcessName} -> Container: {Container}",
                    process.ShortName, process.BlobStorageContainerName);
            }
            else
            {
                // Create FileStorageSource for this process
                fileStorageSource = new Microsoft.Greenlight.Shared.Models.FileStorage.FileStorageSource
                {
                    Id = Guid.NewGuid(),
                    Name = $"Legacy Storage - {process.ShortName}",
                    FileStorageHostId = defaultHost.Id,
                    ContainerOrPath = process.BlobStorageContainerName,
                    AutoImportFolderName = process.BlobStorageAutoImportFolderName ?? "ingest-auto",
                    IsDefault = false,
                    IsActive = true,
                    ShouldMoveFiles = true, // Legacy behavior
                    Description = $"Migrated from legacy blob storage properties for document process: {process.ShortName}"
                };

                dbContext.FileStorageSources.Add(fileStorageSource);
                _logger.LogInformation("Created FileStorageSource for process: {ProcessName} -> Container: {Container}",
                    process.ShortName, process.BlobStorageContainerName);
            }

            // Check if association already exists
            var existingAssociation = await dbContext.DocumentProcessFileStorageSources
                .FirstOrDefaultAsync(dpfs => dpfs.DocumentProcessId == process.Id &&
                                            dpfs.FileStorageSourceId == fileStorageSource.Id, cancellationToken);

            if (existingAssociation == null)
            {
                // Create the association
                var processAssociation = new Microsoft.Greenlight.Shared.Models.FileStorage.DocumentProcessFileStorageSource
                {
                    Id = Guid.NewGuid(),
                    DocumentProcessId = process.Id,
                    FileStorageSourceId = fileStorageSource.Id,
                    Priority = 1,
                    IsActive = true,
                    AcceptsUploads = true
                };

                dbContext.DocumentProcessFileStorageSources.Add(processAssociation);
                _logger.LogInformation("Created association for process: {ProcessName} -> Source: {SourceId}",
                    process.ShortName, fileStorageSource.Id);
            }
            else
            {
                _logger.LogInformation("Association already exists for process: {ProcessName} -> Source: {SourceId}",
                    process.ShortName, fileStorageSource.Id);
            }
        }

        // Migrate DocumentLibrary entities
        var librariesWithLegacyStorage = await dbContext.DocumentLibraries
            .Where(l => !string.IsNullOrEmpty(l.BlobStorageContainerName) &&
                       !l.FileStorageSources.Any())
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} document libraries with legacy blob storage properties to migrate", librariesWithLegacyStorage.Count);

        foreach (var library in librariesWithLegacyStorage)
        {
            // Check if a FileStorageSource already exists for this container/host combination
            var existingSource = await dbContext.FileStorageSources
                .FirstOrDefaultAsync(s => s.FileStorageHostId == defaultHost.Id &&
                                         s.ContainerOrPath == library.BlobStorageContainerName, cancellationToken);

            Microsoft.Greenlight.Shared.Models.FileStorage.FileStorageSource fileStorageSource;

            if (existingSource != null)
            {
                // Use existing source
                fileStorageSource = existingSource;
                _logger.LogInformation("Using existing FileStorageSource for library: {LibraryName} -> Container: {Container}",
                    library.ShortName, library.BlobStorageContainerName);
            }
            else
            {
                // Create FileStorageSource for this library
                fileStorageSource = new Microsoft.Greenlight.Shared.Models.FileStorage.FileStorageSource
                {
                    Id = Guid.NewGuid(),
                    Name = $"Legacy Storage - {library.ShortName}",
                    FileStorageHostId = defaultHost.Id,
                    ContainerOrPath = library.BlobStorageContainerName,
                    AutoImportFolderName = library.BlobStorageAutoImportFolderName ?? "ingest-auto",
                    IsDefault = false,
                    IsActive = true,
                    ShouldMoveFiles = true, // Legacy behavior
                    Description = $"Migrated from legacy blob storage properties for document library: {library.ShortName}"
                };

                dbContext.FileStorageSources.Add(fileStorageSource);
                _logger.LogInformation("Created FileStorageSource for library: {LibraryName} -> Container: {Container}",
                    library.ShortName, library.BlobStorageContainerName);
            }

            // Check if association already exists
            var existingAssociation = await dbContext.DocumentLibraryFileStorageSources
                .FirstOrDefaultAsync(dlfs => dlfs.DocumentLibraryId == library.Id &&
                                            dlfs.FileStorageSourceId == fileStorageSource.Id, cancellationToken);

            if (existingAssociation == null)
            {
                // Create the association
                var libraryAssociation = new Microsoft.Greenlight.Shared.Models.FileStorage.DocumentLibraryFileStorageSource
                {
                    Id = Guid.NewGuid(),
                    DocumentLibraryId = library.Id,
                    FileStorageSourceId = fileStorageSource.Id,
                    Priority = 1,
                    IsActive = true,
                    AcceptsUploads = true
                };

                dbContext.DocumentLibraryFileStorageSources.Add(libraryAssociation);
                _logger.LogInformation("Created association for library: {LibraryName} -> Source: {SourceId}",
                    library.ShortName, fileStorageSource.Id);
            }
            else
            {
                _logger.LogInformation("Association already exists for library: {LibraryName} -> Source: {SourceId}",
                    library.ShortName, fileStorageSource.Id);
            }
        }

        if (processesWithLegacyStorage.Any() || librariesWithLegacyStorage.Any())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully migrated {ProcessCount} document processes and {LibraryCount} document libraries to FileStorageSource architecture",
                processesWithLegacyStorage.Count, librariesWithLegacyStorage.Count);
        }
        else
        {
            _logger.LogInformation("No document processes or libraries found that need legacy blob storage migration");
        }
    }

    private async Task Seed2025_09_04_RebuildAllFileAcknowledgmentRecords(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        // Check if we have corrupted records (containing "ingest-auto") or if this migration has already been run
        // If we find no corrupted records and we have valid FileAcknowledgmentRecords, skip
        var corruptedRecordsCount = await dbContext.FileAcknowledgmentRecords
            .CountAsync(far => far.RelativeFilePath.Contains("ingest-auto"), cancellationToken);

        var totalRecordsCount = await dbContext.FileAcknowledgmentRecords.CountAsync(cancellationToken);

        if (corruptedRecordsCount == 0 && totalRecordsCount > 0)
        {
            _logger.LogInformation("No corrupted FileAcknowledgmentRecords found and {TotalCount} valid records exist. Migration already completed.", totalRecordsCount);
            return;
        }

        _logger.LogInformation("Starting complete rebuild of FileAcknowledgmentRecords from IngestedDocument records");

        // 1. Delete ALL FileAcknowledgmentRecords and their associations
        var acknowledgmentAssociations = await dbContext.IngestedDocumentFileAcknowledgments.ToListAsync(cancellationToken);
        var acknowledgmentRecords = await dbContext.FileAcknowledgmentRecords.ToListAsync(cancellationToken);

        _logger.LogInformation("Deleting {AssociationCount} acknowledgment associations and {RecordCount} acknowledgment records",
            acknowledgmentAssociations.Count, acknowledgmentRecords.Count);

        dbContext.IngestedDocumentFileAcknowledgments.RemoveRange(acknowledgmentAssociations);
        dbContext.FileAcknowledgmentRecords.RemoveRange(acknowledgmentRecords);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 2. Rebuild from IngestedDocuments using correct logic
        var validIngestionStates = new[] {
            IngestionState.FileCopied,
            IngestionState.Processing,
            IngestionState.Complete
        };

        var documentsToMigrate = await dbContext.IngestedDocuments
            .Where(d => !string.IsNullOrEmpty(d.FinalBlobUrl) && validIngestionStates.Contains(d.IngestionState))
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Rebuilding FileAcknowledgmentRecords for {DocumentCount} IngestedDocuments", documentsToMigrate.Count);

        int processedCount = 0;
        int batchSize = 500;
        int totalCount = documentsToMigrate.Count;

        for (int i = 0; i < totalCount; i += batchSize)
        {
            var batch = documentsToMigrate.Skip(i).Take(batchSize);

            foreach (var document in batch)
            {
                try
                {
                    // Get the correct FileStorageSource
                    var fileStorageSourceId = await GetFileStorageSourceForDocumentAsync(dbContext, document, cancellationToken);
                    if (!fileStorageSourceId.HasValue)
                    {
                        // Fall back to default FileStorageSource
                        var defaultSource = await dbContext.FileStorageSources
                            .FirstOrDefaultAsync(s => s.IsDefault && s.IsActive, cancellationToken);
                        if (defaultSource == null)
                        {
                            _logger.LogWarning("No default FileStorageSource found for document {DocumentId} ({FileName}). Skipping.",
                                document.Id, document.FileName);
                            continue;
                        }
                        fileStorageSourceId = defaultSource.Id;
                    }

                    // Extract correct relative path from FinalBlobUrl (not OriginalDocumentUrl!)
                    var relativeFilePath = ExtractRelativePathFromFinalBlobUrl(document.FinalBlobUrl!, fileStorageSourceId.Value, dbContext);
                    var fullFilePath = document.FinalBlobUrl!; // Use the actual current location
                    var fileHash = document.FileHash ?? string.Empty;

                    if (string.IsNullOrEmpty(relativeFilePath))
                    {
                        _logger.LogWarning("Could not determine relative file path for document {DocumentId} ({FileName}). Skipping.",
                            document.Id, document.FileName);
                        continue;
                    }

                    // Create/reuse FileAcknowledgmentRecord with correct paths
                    var acknowledgmentRecord = await dbContext.FileAcknowledgmentRecords
                        .FirstOrDefaultAsync(far => far.FileStorageSourceId == fileStorageSourceId.Value &&
                                                   far.RelativeFilePath == relativeFilePath, cancellationToken);

                    if (acknowledgmentRecord == null)
                    {
                        acknowledgmentRecord = new Microsoft.Greenlight.Shared.Models.FileStorage.FileAcknowledgmentRecord
                        {
                            Id = Guid.NewGuid(),
                            FileStorageSourceId = fileStorageSourceId.Value,
                            RelativeFilePath = relativeFilePath,
                            FileStorageSourceInternalUrl = fullFilePath,
                            FileHash = fileHash,
                            AcknowledgedDate = document.IngestedDate
                        };
                        dbContext.FileAcknowledgmentRecords.Add(acknowledgmentRecord);
                    }

                    // Create the association
                    var association = new Microsoft.Greenlight.Shared.Models.FileStorage.IngestedDocumentFileAcknowledgment
                    {
                        IngestedDocumentId = document.Id,
                        FileAcknowledgmentRecordId = acknowledgmentRecord.Id
                    };
                    dbContext.IngestedDocumentFileAcknowledgments.Add(association);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error rebuilding FileAcknowledgmentRecord for document {DocumentId}", document.Id);
                }
            }

            // Save batch changes
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Processed batch: {BatchProcessed}/{TotalCount} documents rebuilt",
                    Math.Min(i + batchSize, totalCount), totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving batch of rebuilt FileAcknowledgmentRecords");
            }
        }

        // Migration completed successfully

        _logger.LogInformation("Successfully rebuilt {ProcessedCount} FileAcknowledgmentRecords from IngestedDocument records", processedCount);
    }

    private async Task<Guid?> GetFileStorageSourceForDocumentAsync(DocGenerationDbContext dbContext, Microsoft.Greenlight.Shared.Models.IngestedDocument document, CancellationToken cancellationToken)
    {
        try
        {
            if (document.DocumentLibraryType == Microsoft.Greenlight.Shared.Enums.DocumentLibraryType.AdditionalDocumentLibrary)
            {
                // Find FileStorageSource via DocumentLibrary association
                var fileStorageSource = await dbContext.DocumentLibraryFileStorageSources
                    .Include(dlfs => dlfs.DocumentLibrary)
                    .Include(dlfs => dlfs.FileStorageSource)
                    .Where(dlfs => dlfs.DocumentLibrary.ShortName == document.DocumentLibraryOrProcessName &&
                                   dlfs.IsActive)
                    .Select(dlfs => dlfs.FileStorageSource)
                    .FirstOrDefaultAsync(fss => (fss.ContainerOrPath == document.Container ||
                                                (string.IsNullOrEmpty(fss.ContainerOrPath) && document.Container == "default-container")) &&
                                               fss.IsActive, cancellationToken);

                return fileStorageSource?.Id;
            }
            else // PrimaryDocumentProcessLibrary
            {
                // Find FileStorageSource via DocumentProcess association
                var fileStorageSource = await dbContext.DocumentProcessFileStorageSources
                    .Include(dpfs => dpfs.DocumentProcess)
                    .Include(dpfs => dpfs.FileStorageSource)
                    .Where(dpfs => dpfs.DocumentProcess.ShortName == document.DocumentLibraryOrProcessName &&
                                   dpfs.IsActive)
                    .Select(dpfs => dpfs.FileStorageSource)
                    .FirstOrDefaultAsync(fss => (fss.ContainerOrPath == document.Container ||
                                                (string.IsNullOrEmpty(fss.ContainerOrPath) && document.Container == "default-container")) &&
                                               fss.IsActive, cancellationToken);

                return fileStorageSource?.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining FileStorageSource for document {DocumentId}", document.Id);
            return null;
        }
    }



    private string? ExtractRelativePathFromFinalBlobUrl(string finalBlobUrl, Guid fileStorageSourceId, DocGenerationDbContext dbContext)
    {
        try
        {
            // Get the FileStorageSource and its host to understand the base URL structure
            var fileStorageSource = dbContext.FileStorageSources
                .Include(fss => fss.FileStorageHost)
                .FirstOrDefault(fss => fss.Id == fileStorageSourceId);

            if (fileStorageSource?.FileStorageHost == null)
            {
                _logger.LogWarning("FileStorageSource or FileStorageHost not found for FileStorageSourceId {SourceId}", fileStorageSourceId);
                return null;
            }

            var uri = new Uri(finalBlobUrl);

            // For blob storage URLs, the format is typically:
            // https://<account>.blob.core.windows.net/<container>/<path>
            // We want to extract everything after the container name as the relative path

            var pathSegments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
            if (pathSegments.Length >= 2)
            {
                // The first segment should be the container, the second is the relative path
                return pathSegments[1];
            }
            else if (pathSegments.Length == 1)
            {
                // Only container name, no relative path
                return string.Empty;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting relative path from FinalBlobUrl: {Url}", finalBlobUrl);
            return null;
        }
    }

    private async Task Seed2025_09_15_BackfillDisplayFileName(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting DisplayFileName backfill for existing records");

        // Count records that need DisplayFileName backfill
        var acknowledgmentRecordsNeedingBackfill = await dbContext.FileAcknowledgmentRecords
            .CountAsync(far => far.DisplayFileName == null, cancellationToken);

        var ingestedDocumentsNeedingBackfill = await dbContext.IngestedDocuments
            .CountAsync(id => id.DisplayFileName == null, cancellationToken);

        if (acknowledgmentRecordsNeedingBackfill == 0 && ingestedDocumentsNeedingBackfill == 0)
        {
            _logger.LogInformation("No records found needing DisplayFileName backfill. Migration already completed.");
            return;
        }

        _logger.LogInformation("Found {AckCount} FileAcknowledgmentRecords and {DocCount} IngestedDocuments needing DisplayFileName backfill",
            acknowledgmentRecordsNeedingBackfill, ingestedDocumentsNeedingBackfill);

        int totalUpdated = 0;
        int batchSize = 500;

        try
        {
            // Backfill FileAcknowledgmentRecords
            if (acknowledgmentRecordsNeedingBackfill > 0)
            {
                var acknowledgmentRecords = await dbContext.FileAcknowledgmentRecords
                    .Where(far => far.DisplayFileName == null)
                    .ToListAsync(cancellationToken);

                foreach (var record in acknowledgmentRecords)
                {
                    try
                    {
                        // Extract filename from RelativeFilePath
                        if (!string.IsNullOrEmpty(record.RelativeFilePath))
                        {
                            var fileName = System.IO.Path.GetFileName(record.RelativeFilePath);
                            if (!string.IsNullOrEmpty(fileName))
                            {
                                record.DisplayFileName = fileName;
                                totalUpdated++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to extract DisplayFileName from RelativeFilePath '{Path}' for FileAcknowledgmentRecord {Id}. Skipping.",
                            record.RelativeFilePath, record.Id);
                    }
                }

                // Save FileAcknowledgmentRecords in batches
                for (int i = 0; i < acknowledgmentRecords.Count; i += batchSize)
                {
                    try
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                        _logger.LogDebug("Saved batch {BatchStart}-{BatchEnd} of FileAcknowledgmentRecords",
                            i + 1, Math.Min(i + batchSize, acknowledgmentRecords.Count));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving FileAcknowledgmentRecord batch {BatchStart}-{BatchEnd}",
                            i + 1, Math.Min(i + batchSize, acknowledgmentRecords.Count));
                    }
                }

                _logger.LogInformation("Updated DisplayFileName for {Count} FileAcknowledgmentRecords", acknowledgmentRecords.Count(r => !string.IsNullOrEmpty(r.DisplayFileName)));
            }

            // Backfill IngestedDocuments
            if (ingestedDocumentsNeedingBackfill > 0)
            {
                var ingestedDocuments = await dbContext.IngestedDocuments
                    .Where(id => id.DisplayFileName == null)
                    .ToListAsync(cancellationToken);

                foreach (var document in ingestedDocuments)
                {
                    try
                    {
                        // Try to extract from FileName first, then from FinalBlobUrl, then from OriginalDocumentUrl
                        string? displayFileName = null;

                        if (!string.IsNullOrEmpty(document.FileName))
                        {
                            displayFileName = System.IO.Path.GetFileName(document.FileName);
                        }
                        else if (!string.IsNullOrEmpty(document.FinalBlobUrl))
                        {
                            try
                            {
                                var uri = new Uri(document.FinalBlobUrl);
                                var pathSegments = uri.AbsolutePath.TrimStart('/').Split('/');
                                if (pathSegments.Length > 0)
                                {
                                    displayFileName = System.IO.Path.GetFileName(pathSegments[pathSegments.Length - 1]);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Failed to parse FinalBlobUrl '{Url}' for IngestedDocument {Id}",
                                    document.FinalBlobUrl, document.Id);
                            }
                        }
                        else if (!string.IsNullOrEmpty(document.OriginalDocumentUrl))
                        {
                            try
                            {
                                var uri = new Uri(document.OriginalDocumentUrl);
                                var pathSegments = uri.AbsolutePath.TrimStart('/').Split('/');
                                if (pathSegments.Length > 0)
                                {
                                    displayFileName = System.IO.Path.GetFileName(pathSegments[pathSegments.Length - 1]);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Failed to parse OriginalDocumentUrl '{Url}' for IngestedDocument {Id}",
                                    document.OriginalDocumentUrl, document.Id);
                            }
                        }

                        if (!string.IsNullOrEmpty(displayFileName))
                        {
                            document.DisplayFileName = displayFileName;
                            totalUpdated++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to determine DisplayFileName for IngestedDocument {Id}. Skipping.", document.Id);
                    }
                }

                // Save IngestedDocuments in batches
                for (int i = 0; i < ingestedDocuments.Count; i += batchSize)
                {
                    try
                    {
                        await dbContext.SaveChangesAsync(cancellationToken);
                        _logger.LogDebug("Saved batch {BatchStart}-{BatchEnd} of IngestedDocuments",
                            i + 1, Math.Min(i + batchSize, ingestedDocuments.Count));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error saving IngestedDocument batch {BatchStart}-{BatchEnd}",
                            i + 1, Math.Min(i + batchSize, ingestedDocuments.Count));
                    }
                }

                _logger.LogInformation("Updated DisplayFileName for {Count} IngestedDocuments", ingestedDocuments.Count(d => !string.IsNullOrEmpty(d.DisplayFileName)));
            }

            // Final save for any remaining changes
            if (dbContext.ChangeTracker.HasChanges())
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("DisplayFileName backfill completed successfully. Total records updated: {TotalUpdated}", totalUpdated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during DisplayFileName backfill. Some records may not have been updated.");
            // Don't rethrow - this is a best-effort migration that should fail gracefully
        }
    }
}
