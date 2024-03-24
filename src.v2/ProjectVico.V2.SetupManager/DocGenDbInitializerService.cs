using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.SetupManager;

public class DocGenDbInitializerService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<DocGenDbInitializerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly IHostApplicationLifetime _lifetime;

    private Dictionary<Guid, Guid> _chatMessageToConversationMap = new();

    public const string ActivitySourceName = "Migrations";
    private readonly ActivitySource _activitySource = new(ActivitySourceName);

    public DocGenDbInitializerService(
        IServiceProvider sp,
        ILogger<DocGenDbInitializerService> logger,
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        IConfiguration configuration,
        IHostApplicationLifetime lifetime)
    {
        _sp = sp;
        _logger = logger;
        _configuration = configuration;
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _lifetime = lifetime;
    }

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
        using var activity = _activitySource.StartActivity("Initializing Document Generation Database", ActivityKind.Client);
        var sw = Stopwatch.StartNew();

        // If no ChatConversations table exists, reset the ConversationId in ChatMessages to avoid FK constraint violation
        if (!dbContext.Database.GetAppliedMigrations().Any(x => x.Contains("AddedChatConversations")))
        {
            await ResetConversationIdInChatMessages(dbContext, cancellationToken);
        }

        _logger.LogInformation("Temporarily resetting ConversationId in ChatMessages to avoid FK constraint violation");
        
        
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(dbContext.Database.MigrateAsync, cancellationToken);
        sw.Stop();
        _logger.LogInformation("Document Generation Database initialized in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
        activity!.Stop();
    }

    private async Task ResetConversationIdInChatMessages(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        _chatMessageToConversationMap = await dbContext.ChatMessages
            .Where(x => x.ConversationId != Guid.Empty)
            .Select(x => new { x.Id, x.ConversationId })
            .ToDictionaryAsync(x => x.Id, x => x.ConversationId, cancellationToken);

        // Alter the ConversationId column to allow nulls
        //await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE ChatMessages DROP INDEX FK_ChatMessages_ChatConversation_ConversationId", cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE ChatMessages ALTER COLUMN ConversationId UNIQUEIDENTIFIER NULL", cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync("UPDATE ChatMessages SET ConversationId = NULL", cancellationToken);
    }

    private async Task SeedAsync(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Seeding Document Generation Database", ActivityKind.Client);
        _logger.LogInformation("Seeding Document Generation Database started");
        var sw = Stopwatch.StartNew();

        // Seeding logic goes here

        if (!await dbContext.ChatConversations.AnyAsync(cancellationToken))
        {
            // If the ChatConversations table exists, we can assume that the ChatMessages have already been assigned to a ConversationId
            await Seed20230319_FixOrphanedChatMessages(dbContext, cancellationToken);
        }
        
        sw.Stop();
        _logger.LogInformation("Seeding Document Generation Database completed in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
        activity!.Stop();
    }

    private async Task Seed20230319_FixOrphanedChatMessages(DocGenerationDbContext dbContext,
        CancellationToken cancellationToken)
    {

        // Create a ChatConversation for each unique ConversationId in the _chatMessageToConversationMap
        // and assign the ChatMessages to the new ChatConversations

        var distinctConversationIds = _chatMessageToConversationMap.Values.Distinct();
        foreach (var conversationId in distinctConversationIds)
        {
           
            var conversation = new ChatConversation
            {
                Id = conversationId
            };

            dbContext.ChatConversations.Add(conversation);
        }

        await dbContext.SaveChangesAsync(cancellationToken);


        //// Reassign ChatMessages to ChatConversations
        //foreach (var (chatMessageId, conversationId) in _chatMessageToConversationMap)
        //{
        //    var chatMessage = await dbContext.ChatMessages.FindAsync(chatMessageId, cancellationToken);
        //    chatMessage.ConversationId = conversationId;
        //    dbContext.Update(chatMessage);
        //}

        //await dbContext.SaveChangesAsync(cancellationToken);

        
    }
}
