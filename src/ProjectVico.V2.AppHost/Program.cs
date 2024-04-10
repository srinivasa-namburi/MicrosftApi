using Aspire.Hosting.Azure;
using Microsoft.Extensions.Configuration;
using ProjectVico.V2.AppHost;


var builder = DistributedApplication.CreateBuilder(args);
AppHostConfigurationSetup(builder);

var envServiceConfigurationConfigurationSection = builder.Configuration.GetSection("ServiceConfiguration");
var envAzureAdConfigurationSection = builder.Configuration.GetSection("AzureAd");
var envConnectionStringsConfigurationSection = builder.Configuration.GetSection("ConnectionStrings");

// Used to determine service configuration.
var durableDevelopment = Convert.ToBoolean(builder.Configuration["ServiceConfiguration:ProjectVicoServices:DocumentGeneration:DurableDevelopmentServices"]);

// The default password for SQL server is in appsettings.json. You can override it in appsettings.Development.json.
// User name is "sa" and you must use SQL server authentication (not Azure AD/Windows authentication).
var sqlPassword = builder.AddParameter("sqlPassword", true);
var sqlDatabaseName = builder.Configuration["ServiceConfiguration:SQL:DatabaseName"];

IResourceBuilder<SqlServerDatabaseResource> docGenSql;
IResourceBuilder<AzureServiceBusResource>? sbus;
IResourceBuilder<IResourceWithConnectionString> queueService;
IResourceBuilder<RedisResource> redis;

//var signalr = builder.ExecutionContext.IsPublishMode
//    ? builder.AddAzureSignalR("signalr")
//    : builder.AddConnectionString("signalr");

var signalr = builder.AddAzureSignalR("signalr");

if (builder.ExecutionContext.IsRunMode) // For local development
{
    if (durableDevelopment)
    {
        redis = builder
            .AddRedis("redis", 16379)
            .WithDataVolume("pvico-redis-vol")
            .WithPersistence()
            ;

        docGenSql = builder
            .AddSqlServer("sqldocgen", password: sqlPassword, port: 9001)
            .WithDataVolume("pvico-sql-docgen-vol")
            .AddDatabase(sqlDatabaseName);


    }
    else // Don't persist data and queue content - it will be deleted on restart!
    {
        docGenSql = builder
            .AddSqlServer("sqldocgen", password: sqlPassword, 9001)
            .AddDatabase(sqlDatabaseName);

        redis = builder.AddRedis("redis", 16379);


    }

}
else // For production/Azure deployment
{
    docGenSql = builder
        .AddSqlServer("sqldocgen")
        .PublishAsAzureSqlDatabase()
        .AddDatabase(sqlDatabaseName);

    redis = builder.AddRedis("redis")
        .PublishAsAzureRedis()
        .WithPersistence();
}

// Azure Service Bus is used by all variations of configurations

sbus = builder.AddAzureServiceBus("sbus");
queueService = sbus;

var apiMain = builder
    .AddProject<Projects.ProjectVico_V2_API_Main>("api-main")
    //.WithHttpsEndpoint(6001)
    .WithExternalHttpEndpoints()
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(signalr)
    .WithReference(redis)
    .WithReference(docGenSql)
    .WithReference(queueService);

var workerDocumentGeneration = builder
    .AddProject<Projects.ProjectVico_V2_Worker_DocumentGeneration>("worker-documentgeneration")
    .WithReplicas(Convert.ToUInt16(
        builder.Configuration["ServiceConfiguration:ProjectVicoServices:DocumentGeneration:NumberOfGenerationWorkers"]))
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(docGenSql)
    .WithReference(queueService)
    .WithReference(apiMain)
    ;

var workerChat = builder.AddProject<Projects.ProjectVico_V2_Worker_Chat>("worker-chat")
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(docGenSql)
    .WithReference(queueService)
    .WithReference(apiMain);

var workerDocumentIngestion = builder
    .AddProject<Projects.ProjectVico_V2_Worker_DocumentIngestion>("worker-documentingestion")
    .WithReplicas(Convert.ToUInt16(
        builder.Configuration["ServiceConfiguration:ProjectVicoServices:DocumentIngestion:NumberOfIngestionWorkers"]))
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(docGenSql)
    .WithReference(queueService)
    .WithReference(apiMain);

var workerScheduler = builder
    .AddProject<Projects.ProjectVico_V2_Worker_Scheduler>("worker-scheduler")
    .WithReplicas(1) // There can only be one Scheduler
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(docGenSql)
    .WithReference(queueService)
    .WithReference(apiMain);

var setupManager = builder
    .AddProject<Projects.ProjectVico_V2_SetupManager>("worker-setupmanager")
    .WithReplicas(1) // There can only be one Setup Manager
    .WithReference(queueService)
    .WithReference(docGenSql)
    .WithConfigSection(envServiceConfigurationConfigurationSection);

var docGenFrontend = builder
    .AddProject<Projects.ProjectVico_V2_Web_DocGen>("web-docgen")
    //.WithHttpsEndpoint(5001)
    .WithExternalHttpEndpoints()
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(signalr)
    .WithReference(redis)
    .WithReference(apiMain);

builder.Build().Run();

void AppHostConfigurationSetup(IDistributedApplicationBuilder distributedApplicationBuilder)
{

    distributedApplicationBuilder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

    if (distributedApplicationBuilder.ExecutionContext.IsRunMode)
    {
        distributedApplicationBuilder.Configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
        distributedApplicationBuilder.Configuration.AddUserSecrets<Program>();
    }
    else
    {
        distributedApplicationBuilder.Configuration.AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: true);
    }

    distributedApplicationBuilder.Configuration.AddEnvironmentVariables();
}