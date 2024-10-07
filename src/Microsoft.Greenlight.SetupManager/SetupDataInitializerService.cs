using System.Diagnostics;
using Azure;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.DocumentProcess.Shared.Search;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models.Plugins;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;

namespace Microsoft.Greenlight.SetupManager;

public class SetupDataInitializerService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<SetupDataInitializerService> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    private IDocumentProcessInfoService _documentProcessInfoService;
    private readonly SearchClientFactory _searchClientFactory;

    public const string ActivitySourceName = "Migrations";
    private readonly ActivitySource _activitySource = new(ActivitySourceName);

    public SetupDataInitializerService(
        IServiceProvider sp,
        ILogger<SetupDataInitializerService> logger,
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        IConfiguration configuration,
        IHostApplicationLifetime lifetime,
        SearchClientFactory searchClientFactory)
    {
        _sp = sp;
        _logger = logger;
        _lifetime = lifetime;
        _searchClientFactory = searchClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = _sp.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<DocGenerationDbContext>();
        var documentProcessInfoService = scope.ServiceProvider.GetRequiredService<IDocumentProcessInfoService>();

        await InitializeDatabaseAsync(dbContext, cancellationToken);
        await SeedAsync(dbContext, cancellationToken);
        await CreateKernelMemoryIndexes(documentProcessInfoService, cancellationToken);

        _lifetime.StopApplication();
    }

    private async Task InitializeDatabaseAsync(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Initializing Document Generation Database", ActivityKind.Client);
        var sw = Stopwatch.StartNew();

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(dbContext.Database.MigrateAsync, cancellationToken);
        sw.Stop();
        _logger.LogInformation("Document Generation Database initialized in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
        activity!.Stop();
    }

    private async Task SeedAsync(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Seeding Document Generation Database", ActivityKind.Client);
        _logger.LogInformation("Seeding Document Generation Database started");
        var sw = Stopwatch.StartNew();

        await Seed2024_04_07_IngestedDocumentDocumentProcess(dbContext, cancellationToken);
        await Seed2024_05_24_OrphanedChatMessagesCleanup(dbContext, cancellationToken);
        await Seed2024_05_24_ChatConversationsWithNoMessagesCleanup(dbContext, cancellationToken);
        //await Seed2024_10_01_CreateDummyPlugin(dbContext, cancellationToken);

        sw.Stop();
        _logger.LogInformation("Seeding Document Generation Database completed in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
        activity!.Stop();
    }

    private async Task CreateKernelMemoryIndexes(IDocumentProcessInfoService documentProcessInfoService,
        CancellationToken cancellationToken)
    {
        var documentProcesses = await documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
        var kernelMemoryDocumentProcesses = documentProcesses.Where(x => x.LogicType == DocumentProcessLogicType.KernelMemory).ToList();

        if (kernelMemoryDocumentProcesses.Count == 0)
        {
            _logger.LogInformation("No Kernel Memory-based Document Processes found. Skipping index creation.");
            return;
        }

        _logger.LogInformation("Creating Kernel Memory indexes for {Count} Document Processes", kernelMemoryDocumentProcesses.Count);

        // For each Kernel Memory-based Document Process, create the necessary indexes if they don't already exist
        foreach (var documentProcess in kernelMemoryDocumentProcesses)
        {
            var kernelMemoryRepository = _sp
                .GetServiceForDocumentProcess<IKernelMemoryRepository>(documentProcess.ShortName);

            if (kernelMemoryRepository == null)
            {
                _logger.LogError("No Kernel Memory repository registered for Document Process {DocumentProcess} - skipping", documentProcess.ShortName);
                continue;
            }

            foreach (var repository in documentProcess.Repositories)
            {
                // Check if the index already exists. If it does, skip it.
                var searchIndexClient = _searchClientFactory.GetSearchIndexClientForIndex(repository);
                var indexAlreadyExists = true;
                
                try
                {
                    var index = await searchIndexClient.GetIndexAsync(repository, cancellationToken);
                }
                catch(RequestFailedException e)
                {
                    // The AI Search API returns a 404 status code if the index does not exist
                    if (e.Status == 404)
                    {
                        indexAlreadyExists = false;
                    }
                }
                
                if (indexAlreadyExists)
                {
                    _logger.LogInformation("Index {IndexName} already exists for Document Process {DocumentProcess}. Skipping creation.", repository, documentProcess.ShortName);
                    continue;
                }

                var currentTimeUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var dummyDocumentCreatedFileName = $"DummyDocument-{currentTimeUnixTime}.pdf";
                _logger.LogInformation("Creating index for Document Process {DocumentProcess} and Repository {Repository}", documentProcess.ShortName, repository);
                // Create a stream from the file DummyDocument.pdf in the current directory
                var fileStream = File.OpenRead("DummyDocument.pdf");
                // The indexes are created automatically on upload of a document. Use the repository to upload the dummy document
                await kernelMemoryRepository.StoreContentAsync(documentProcess.ShortName, repository, fileStream, dummyDocumentCreatedFileName, null);
                fileStream.Close();

                _logger.LogInformation("Index created for Document Process {DocumentProcess} and Repository {Repository}", documentProcess.ShortName, repository);

                // Delete the dummy document after the index is created
                await kernelMemoryRepository.DeleteContentAsync(documentProcess.ShortName, repository, dummyDocumentCreatedFileName);
            }
        }
    }


    private async Task Seed2024_04_07_IngestedDocumentDocumentProcess(DocGenerationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Set Document Process to "US.NuclearLicensing" on IngestedDocuments where DocumentProcess is null
        // First, get a count of the number of IngestedDocuments where DocumentProcess is null. If it's 0, we don't need to do anything.

        var count = await dbContext.IngestedDocuments
            .Where(x => x.DocumentProcess == null)
            .CountAsync(cancellationToken);

        if (count == 0)
        {
            _logger.LogInformation("No IngestedDocuments found where DocumentProcess is null. Skipping seeding logic.");
            return;
        }


        _logger.LogInformation("Seeding : Setting Document Process to 'US.NuclearLicensing' on {Count} IngestedDocuments where DocumentProcess is null", count);

        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE IngestedDocuments SET DocumentProcess = {0} WHERE DocumentProcess IS NULL",
            "US.NuclearLicensing");

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task Seed2024_05_24_OrphanedChatMessagesCleanup(DocGenerationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Remove any ChatMessages that are not associated with a ChatConversation
        // First, get a count of the number of ChatMessages that are not associated with a ChatConversation. If it's 0, we don't need to do anything.

        // The ChatMessages currently have a ConversationId in the model that is not nullable, so we can't have a null ConversationId.
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
        // First, get a count of the number of ChatConversations that have no associated ChatMessages. If it's 0, we don't need to do anything.

        // The ChatConversations currently have a list of ChatMessages in the model,
        // so we can use LINQ to find ChatConversations with no associated ChatMessages

        var expirePoint = DateTime.UtcNow - (7.Days());

        var count = await dbContext.ChatConversations
            .Where(x => x.ChatMessages.Count == 0)
            .Where(x => x.CreatedAt < expirePoint)
            .CountAsync(cancellationToken);

        if (count == 0)
        {
            _logger.LogInformation("No (old) ChatConversations found with no associated ChatMessages. Skipping cleanup logic.");
            return;
        }

        _logger.LogInformation("Cleaning up : Removing {Count} ChatConversations with no associated ChatMessages that are older than 7 days", count);

        var chatConversations = await dbContext.ChatConversations
            .Where(x => x.ChatMessages.Count == 0)
            .Where(x => x.CreatedAt < expirePoint)
            .ToListAsync(cancellationToken);

        // This marks the ChatConversations for deletion through their IsActive property
        dbContext.ChatConversations.RemoveRange(chatConversations);
        await dbContext.SaveChangesAsync(cancellationToken);

        // This actually deletes the ChatConversations from the database
        await dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM ChatConversations WHERE IsActive = 0", cancellationToken: cancellationToken);

    }

    private async Task Seed2024_10_01_CreateDummyPlugin(DocGenerationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var plugin = new DynamicPlugin
        {
            Id = Guid.Parse("a63fbbac-fbc1-4d23-ac98-0367a22c78df"),
            Name = "Microsoft.Greenlight.Demos.PluginDemo",
            BlobContainerName = "plugintest",
            Versions = [
                new DynamicPluginVersion(1, 0, 0)
            ]
        };

        if (await dbContext.DynamicPlugins.AnyAsync(x => x.Name == plugin.Name, cancellationToken))
        {
            _logger.LogInformation("Plugin {PluginName} already exists. Skipping creation.", plugin.Name);
            return;
        }
        else
        {
            dbContext.DynamicPlugins.Add(plugin);
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Plugin {PluginName} with version {PluginVersion} created.", plugin.Name, plugin.LatestVersion);
        }
    }
}
