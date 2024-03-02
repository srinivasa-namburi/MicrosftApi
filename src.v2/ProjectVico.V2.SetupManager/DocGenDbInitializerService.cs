using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ProjectVico.V2.Shared.Data.Sql;

namespace ProjectVico.V2.SetupManager;

public class DocGenDbInitializerService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<DocGenDbInitializerService> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public const string ActivitySourceName = "Migrations";
    private readonly ActivitySource _activitySource = new(ActivitySourceName);

    public DocGenDbInitializerService(IServiceProvider sp, ILogger<DocGenDbInitializerService> logger, IHostApplicationLifetime lifetime)
    {
        _sp = sp;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = _sp.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DocGenerationDbContext>();

        await InitializeDatabaseAsync(dbContext, cancellationToken);
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

        await SeedAsync(dbContext, cancellationToken);
    }

    private async Task SeedAsync(DocGenerationDbContext dbContext, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("Seeding Document Generation Database", ActivityKind.Client);
        _logger.LogInformation("Seeding Document Generation Database started");
        var sw = Stopwatch.StartNew();

        // Seeding logic goes here

        await dbContext.SaveChangesAsync(cancellationToken);

        sw.Stop();
        _logger.LogInformation("Seeding Document Generation Database completed in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
        activity!.Stop();
    }
}
