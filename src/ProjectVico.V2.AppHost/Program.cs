using Aspire.Hosting.Azure;
using Azure.ResourceManager.Redis.Models;
using Azure.ResourceManager.Search.Models;
using Azure.ResourceManager.ServiceBus.Models;
using Azure.ResourceManager.SignalR.Models;
using Microsoft.Extensions.Configuration;
using ProjectVico.V2.AppHost;
#pragma warning disable ASPIRE0001

var builder = DistributedApplication.CreateBuilder(args);
AppHostConfigurationSetup(builder);

var envServiceConfigurationConfigurationSection = builder.Configuration.GetSection("ServiceConfiguration");
var envAzureAdConfigurationSection = builder.Configuration.GetSection("AzureAd");
var envConnectionStringsConfigurationSection = builder.Configuration.GetSection("ConnectionStrings");

// Used to determine service configuration.
var durableDevelopment = Convert.ToBoolean(builder.Configuration["ServiceConfiguration:ProjectVicoServices:DocumentGeneration:DurableDevelopmentServices"]);

// Set the Parameters:SqlPassword Key is user secrets (right click AppHost project, select User Secrets, add the key and value)
// Example: "Parameters:sqlPassword": "password"

var sqlPassword = builder.AddParameter("sqlPassword", true);
var sqlDatabaseName = builder.Configuration["ServiceConfiguration:SQL:DatabaseName"];

IResourceBuilder<SqlServerDatabaseResource> docGenSql;
IResourceBuilder<AzureServiceBusResource>? sbus;
IResourceBuilder<IResourceWithConnectionString> queueService;
IResourceBuilder<RedisResource> redis;

var signalr = builder.AddAzureSignalR("signalr", (resourceBuilder, construct, options) =>
{
    if(builder.ExecutionContext.IsRunMode)
    {
        options.Properties.Sku.Tier = SignalRSkuTier.Standard;
        options.Properties.Sku.Name = "Standard_S1";
        options.Properties.Sku.Capacity = 1;
    }
    else
    {
        options.Properties.Sku.Tier = SignalRSkuTier.Premium;
        options.Properties.Sku.Name = "Premium_P1";
        options.Properties.Sku.Capacity = 3;
    }
});

var blobStorage = builder
    .AddAzureStorage("docing")
    .AddBlobs("blob-docing");

var azureAiSearch = builder.AddAzureSearch("aiSearch", (resourceBuilder, construct, options) =>
{
    if (builder.ExecutionContext.IsRunMode)
    {
        options.Properties.SkuName = SearchSkuName.Basic;
    }
    else
    {
        options.Properties.SkuName = SearchSkuName.Standard;
        options.Properties.ReplicaCount = 2;
        options.Properties.PartitionCount = 2;
    }
});

if (builder.ExecutionContext.IsRunMode) // For local development
{
    if (durableDevelopment)
    {
        redis = builder
            .AddRedis("redis", 16379)
            .WithDataVolume("pvico-redis-vol")
            .WithPersistence();

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

    sbus = builder.AddAzureServiceBus("sbus");
}
else // For production/Azure deployment
{
    docGenSql = builder
        .AddSqlServer("sqldocgen")
        .PublishAsAzureSqlDatabase()
        .AddDatabase(sqlDatabaseName);

    redis = builder.AddRedis("redis")
        .PublishAsAzureRedis((resourceBuilder, construct, options) =>
        {
            options.Properties.Sku.Name = RedisSkuName.Standard;
            options.Properties.Sku.Family = RedisSkuFamily.BasicOrStandard;
            options.Properties.Sku.Capacity = 1;
        })
        .WithPersistence();

    sbus = builder.AddAzureServiceBus("sbus", (resourceBuilder, construct, options) =>
    {
        options.Properties.Sku.Name = ServiceBusSkuName.Premium;
        options.Properties.Sku.Tier = ServiceBusSkuTier.Premium;
        options.Properties.Sku.Capacity = 1;
    });

}

queueService = sbus;

var apiMain = builder
    .AddProject<Projects.ProjectVico_V2_API_Main>("api-main")
    .WithExternalHttpEndpoints()
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(blobStorage)
    .WithReference(signalr)
    .WithReference(redis)
    .WithReference(docGenSql)
    .WithReference(queueService);

var docGenFrontend = builder
    .AddProject<Projects.ProjectVico_V2_Web_DocGen>("web-docgen")
    .WithExternalHttpEndpoints()
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(blobStorage)
    .WithReference(signalr)
    .WithReference(redis)
    .WithReference(apiMain);

apiMain.WithReference(docGenFrontend); // Neccessary for CORS policy creation

var setupManager = builder
    .AddProject<Projects.ProjectVico_V2_SetupManager>("worker-setupmanager")
    .WithReplicas(1) // There can only be one Setup Manager
    .WithReference(azureAiSearch)
    .WithReference(queueService)
    .WithReference(docGenSql)
    .WithConfigSection(envServiceConfigurationConfigurationSection);

var workerScheduler = builder
    .AddProject<Projects.ProjectVico_V2_Worker_Scheduler>("worker-scheduler")
    .WithReplicas(1) // There can only be one Scheduler
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(blobStorage)
    .WithReference(docGenSql)
    .WithReference(queueService)
    .WithReference(apiMain);

var workerDocumentGeneration = builder
    .AddProject<Projects.ProjectVico_V2_Worker_DocumentGeneration>("worker-documentgeneration")
    .WithReplicas(Convert.ToUInt16(
        builder.Configuration["ServiceConfiguration:ProjectVicoServices:DocumentGeneration:NumberOfGenerationWorkers"]))
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(azureAiSearch)
    .WithReference(blobStorage)
    .WithReference(docGenSql)
    .WithReference(queueService)
    .WithReference(apiMain)
    ;

var workerDocumentIngestion = builder
    .AddProject<Projects.ProjectVico_V2_Worker_DocumentIngestion>("worker-documentingestion")
    .WithReplicas(Convert.ToUInt16(
        builder.Configuration["ServiceConfiguration:ProjectVicoServices:DocumentIngestion:NumberOfIngestionWorkers"]))
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(azureAiSearch)
    .WithReference(blobStorage)
    .WithReference(docGenSql)
    .WithReference(queueService)
    .WithReference(apiMain);

var workerChat = builder.AddProject<Projects.ProjectVico_V2_Worker_Chat>("worker-chat")
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(azureAiSearch)
    .WithReference(blobStorage)
    .WithReference(docGenSql)
    .WithReference(queueService)
    .WithReference(apiMain);

builder.Build().Run();

void AppHostConfigurationSetup(IDistributedApplicationBuilder distributedApplicationBuilder)
{

    distributedApplicationBuilder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

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