using Microsoft.Extensions.Configuration;
using Microsoft.Greenlight.AppHost;


var builder = DistributedApplication.CreateBuilder(args);
AppHostConfigurationSetup(builder);

var envServiceConfigurationConfigurationSection = builder.Configuration.GetSection("ServiceConfiguration");
var envAzureAdConfigurationSection = builder.Configuration.GetSection("AzureAd");
var envConnectionStringsConfigurationSection = builder.Configuration.GetSection("ConnectionStrings");
var envAzureConfigurationSection = builder.Configuration.GetSection("Azure");
var envKestrelConfigurationSection = builder.Configuration.GetSection("Kestrel");

// Used to determine service configuration.
var durableDevelopment =
    Convert.ToBoolean(
        builder.Configuration["ServiceConfiguration:GreenlightServices:DocumentGeneration:DurableDevelopmentServices"]
    );

// Set the Parameters:SqlPassword Key is user secrets (right click AppHost project, select User Secrets, add the key
// and value)
// Example: "Parameters:sqlPassword": "password"

// Change from ADO
var sqlPassword = builder.AddParameter("sqlPassword", true);
var sqlDatabaseName = builder.Configuration["ServiceConfiguration:SQL:DatabaseName"];

IResourceBuilder<IResourceWithConnectionString> docGenSql;
IResourceBuilder<IResourceWithConnectionString> sbus;
IResourceBuilder<IResourceWithConnectionString> redisResource;
IResourceBuilder<IResourceWithConnectionString> signalr;
IResourceBuilder<IResourceWithConnectionString> azureAiSearch;
IResourceBuilder<IResourceWithConnectionString> blobStorage;
IResourceBuilder<IResourceWithConnectionString> insights = null!;

if (builder.ExecutionContext.IsRunMode) // For local development
{
    if (durableDevelopment)
    {
        redisResource = builder.AddRedis("redis", 16379)
            .WithDataVolume("pvico-redis-vol")
            .WithLifetime(ContainerLifetime.Persistent);

        // Use Azure SQL Server for local development.
        // Especially useful for ARM/AMD based machines that can't run SQL Server in a container
        var useAzureSqlServer = Convert.ToBoolean(
            builder.Configuration["ServiceConfiguration:GreenlightServices:Global:UseAzureSqlServer"]);

        if (useAzureSqlServer)
        {
            docGenSql = builder
             .AddAzureSqlServer("sqldocgen")
             .AddDatabase(sqlDatabaseName!);
        }
        else
        {
            docGenSql = builder
            .AddSqlServer("sqldocgen", password: sqlPassword, port: 9001)
            .WithDataVolume("pvico-sql-docgen-vol")
            .WithLifetime(ContainerLifetime.Persistent)
            .AddDatabase(sqlDatabaseName!);
        }
    }
    else // Don't persist data and queue content - it will be deleted on restart!
    {
        docGenSql = builder
            .AddSqlServer("sqldocgen", password: sqlPassword, 9001)
            .AddDatabase(sqlDatabaseName!);

        redisResource = builder.AddRedis("redis", 16379);
    }

    // The following resources are either deployed through the Aspire Azure Resource Manager or connected via
    // connection string.
    // Use the connection string to connect to the resources is the configuration key "Azure:SubscriptionId" is not
    // set.
    if (string.IsNullOrEmpty(builder.Configuration["Azure:SubscriptionId"]))
    {
        signalr = builder.AddConnectionString("signalr");
        sbus = builder.AddConnectionString("sbus");
        azureAiSearch = builder.AddConnectionString("aiSearch");
        blobStorage = builder.AddConnectionString("blob-docing");

        // Only add Application Insights if the connection string is set
        if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
            insights = builder.AddConnectionString("insights", "APPLICATIONINSIGHTS_CONNECTION_STRING");
    }
    else
    {
        signalr = builder.AddAzureSignalR("signalr");
        sbus = builder.AddAzureServiceBus("sbus");
        azureAiSearch = builder.AddAzureSearch("aiSearch");

        blobStorage = builder
            .AddAzureStorage("docing")
            .AddBlobs("blob-docing");

        // Add Application insights when configured to do so
        var useAppInsights = Convert.ToBoolean(
            builder.Configuration["ServiceConfiguration:GreenlightServices:Global:UseApplicationInsights"]);
        if (useAppInsights)
            insights = builder.AddAzureApplicationInsights("insights");
    }
}
else // For production/Azure deployment
{
    docGenSql = builder.AddAzureSqlServer("sqldocgen").AddDatabase(sqlDatabaseName!);
    redisResource = builder.AddAzureRedis("redis");    
    sbus = builder.AddAzureServiceBus("sbus");
    azureAiSearch = builder.AddAzureSearch("aiSearch");    
    signalr = builder.AddAzureSignalR("signalr");    
    blobStorage = builder.AddAzureStorage("docing").AddBlobs("blob-docing");
    insights = builder.AddAzureApplicationInsights("insights");
}

var dbSetupManager = builder
    .AddProject<Projects.Microsoft_Greenlight_SetupManager_DB>("db-setupmanager")
    .WithReplicas(1) // There can only be one Setup Manager
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithConfigSection(envAzureConfigurationSection)
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithReference(docGenSql)
    .WaitFor(docGenSql);

var apiMain = builder
    .AddProject<Projects.Microsoft_Greenlight_API_Main>("api-main")
    .WithExternalHttpEndpoints()
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithConfigSection(envAzureConfigurationSection)
    .WithConfigSection(envKestrelConfigurationSection)
    .WithReference(blobStorage)
    .WithReference(signalr)
    .WithReference(redisResource)
    .WithReference(docGenSql)
    .WithReference(sbus)
    .WithReference(azureAiSearch)
    .WaitForCompletion(dbSetupManager);

var servicesSetupManager = builder
    .AddProject<Projects.Microsoft_Greenlight_SetupManager_Services>("services-setupmanager")
    .WithReplicas(1) // There can only be one Setup Manager
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithConfigSection(envAzureConfigurationSection)
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithReference(azureAiSearch)
    .WithReference(blobStorage)
    .WithReference(sbus)
    .WithReference(docGenSql)
    .WithReference(redisResource)
    .WithReference(apiMain)
    .WaitForCompletion(dbSetupManager);

var docGenFrontend = builder
    .AddProject<Projects.Microsoft_Greenlight_Web_DocGen>("web-docgen")
    .WithExternalHttpEndpoints()
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithConfigSection(envAzureConfigurationSection)
    .WithConfigSection(envKestrelConfigurationSection)
    .WithReference(blobStorage)
    .WithReference(docGenSql)
    .WithReference(signalr)
    .WithReference(redisResource)
    .WithReference(apiMain)
    .WaitForCompletion(dbSetupManager);

apiMain.WithReference(docGenFrontend); // Neccessary for CORS policy creation

var workerScheduler = builder
    .AddProject<Projects.Microsoft_Greenlight_Worker_Scheduler>("worker-scheduler")
    .WithReplicas(1) // There can only be one Scheduler
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithConfigSection(envAzureConfigurationSection)
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithReference(blobStorage)
    .WithReference(docGenSql)
    .WithReference(sbus)
    .WithReference(redisResource)
    .WithReference(apiMain)
    .WithReference(azureAiSearch)
    .WaitForCompletion(dbSetupManager);

var workerDocumentGeneration = builder
    .AddProject<Projects.Microsoft_Greenlight_Worker_DocumentGeneration>("worker-documentgeneration")
    .WithReplicas(Convert.ToUInt16(
        builder.Configuration["ServiceConfiguration:GreenlightServices:DocumentGeneration:NumberOfGenerationWorkers"]))
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithConfigSection(envAzureConfigurationSection)
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithReference(azureAiSearch)
    .WithReference(blobStorage)
    .WithReference(docGenSql)
    .WithReference(sbus)
    .WithReference(redisResource)
    .WithReference(apiMain)
    .WaitForCompletion(dbSetupManager);

var workerDocumentIngestion = builder
    .AddProject<Projects.Microsoft_Greenlight_Worker_DocumentIngestion>("worker-documentingestion")
    .WithReplicas(Convert.ToUInt16(
        builder.Configuration["ServiceConfiguration:GreenlightServices:DocumentIngestion:NumberOfIngestionWorkers"]))
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithConfigSection(envAzureConfigurationSection)
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithReference(azureAiSearch)
    .WithReference(blobStorage)
    .WithReference(docGenSql)
    .WithReference(sbus)
    .WithReference(redisResource)
    .WithReference(apiMain)
    .WaitForCompletion(dbSetupManager);

var workerChat = builder.AddProject<Projects.Microsoft_Greenlight_Worker_Chat>("worker-chat")
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithConfigSection(envAzureConfigurationSection)
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithReference(azureAiSearch)
    .WithReference(blobStorage)
    .WithReference(docGenSql)
    .WithReference(sbus)
    .WithReference(redisResource)
    .WithReference(apiMain)
    .WaitForCompletion(dbSetupManager);

var workerValidation = builder.AddProject<Projects.Microsoft_Greenlight_Worker_Validation>("worker-validation")
    .WithReplicas(Convert.ToUInt16(
        builder.Configuration["ServiceConfiguration:GreenlightServices:DocumentValidation:NumberOfValidationWorkers"]))
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithConfigSection(envAzureConfigurationSection)
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithReference(azureAiSearch)
    .WithReference(blobStorage)
    .WithReference(docGenSql)
    .WithReference(sbus)
    .WithReference(redisResource)
    .WithReference(apiMain)
    .WaitForCompletion(dbSetupManager);

if(insights is not null)
{
    apiMain.WithReference(insights);
    workerScheduler.WithReference(insights);
    workerDocumentGeneration.WithReference(insights);
    workerDocumentIngestion.WithReference(insights);
    workerChat.WithReference(insights);
}



builder.Build().Run();

static void AppHostConfigurationSetup(IDistributedApplicationBuilder distributedApplicationBuilder)
{
    distributedApplicationBuilder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

    if (distributedApplicationBuilder.ExecutionContext.IsRunMode)
    {
        distributedApplicationBuilder
            .Configuration
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
        distributedApplicationBuilder.Configuration.AddUserSecrets<Program>();
    }
    else
    {
        distributedApplicationBuilder
            .Configuration
            .AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: true);
    }

    distributedApplicationBuilder.Configuration.AddEnvironmentVariables();
}
