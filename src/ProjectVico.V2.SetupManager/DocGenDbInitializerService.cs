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
        
        sw.Stop();
        _logger.LogInformation("Seeding Document Generation Database completed in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
        activity!.Stop();
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
}
