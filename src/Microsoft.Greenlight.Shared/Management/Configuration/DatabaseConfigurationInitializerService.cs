   using Microsoft.EntityFrameworkCore;
   using Microsoft.Extensions.Configuration;
   using Microsoft.Extensions.Hosting;
   using Microsoft.Extensions.Logging;
   using Microsoft.Greenlight.Shared.Data.Sql;
   using Microsoft.Greenlight.Shared.Management.Configuration;

   namespace Microsoft.Greenlight.Shared.Management.Configuration;

   /// <summary>
   /// A hosted service that ensures database configuration is loaded after application startup.
   /// </summary>
   public class DatabaseConfigurationInitializerService : IHostedService
   {
       private readonly EfCoreConfigurationProvider _configProvider;
       private readonly DocGenerationDbContext _dbContext;
       private readonly ILogger<DatabaseConfigurationInitializerService> _logger;

       /// <summary>
       /// Initializes a new instance of the <see cref="DatabaseConfigurationInitializerService"/> class.
       /// </summary>
       /// <param name="configProvider">The configuration provider.</param>
       /// <param name="dbContext">The DB context.</param>
       /// <param name="logger">The logger.</param>
       public DatabaseConfigurationInitializerService(
           EfCoreConfigurationProvider configProvider,
           DocGenerationDbContext dbContext,
           ILogger<DatabaseConfigurationInitializerService> logger)
       {
           _configProvider = configProvider;
           _dbContext = dbContext;
           _logger = logger;
       }

       /// <summary>
       /// Starts the service, ensuring database configuration is properly initialized.
       /// </summary>
       /// <param name="cancellationToken">The cancellation token.</param>
       /// <returns>A task representing the asynchronous operation.</returns>
       public Task StartAsync(CancellationToken cancellationToken)
       {
           try
           {
               _logger.LogInformation("Initializing database configuration provider");
               
               // Load the initial data
               _configProvider.Load();
               _logger.LogInformation("Database configuration provider initialized successfully");
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Error initializing database configuration provider");
           }
           
           return Task.CompletedTask;
       }

       /// <summary>
       /// Stops the service.
       /// </summary>
       /// <param name="cancellationToken">The cancellation token.</param>
       /// <returns>A task representing the asynchronous operation.</returns>
       public Task StopAsync(CancellationToken cancellationToken)
       {
           return Task.CompletedTask;
       }
   }
   