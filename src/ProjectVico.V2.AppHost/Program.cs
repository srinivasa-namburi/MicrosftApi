using Aspire.Hosting.Azure;
using Microsoft.Extensions.Configuration;
using ProjectVico.V2.AppHost;


var builder = DistributedApplication.CreateBuilder(args);
AppHostConfigurationSetup(builder);

//TODO: Add Azure Provisioning
//Reads needed details from Configuration provider.
//See sample : https://github.com/dotnet/aspire/blob/main/playground/AzureSearchEndToEnd/AzureSearch.AppHost/appsettings.json
//builder.AddAzureProvisioning();

var envServiceConfigurationConfigurationSection = builder.Configuration.GetSection("ServiceConfiguration");
var envAzureAdConfigurationSection = builder.Configuration.GetSection("AzureAd");
var envConnectionStringsConfigurationSection = builder.Configuration.GetSection("ConnectionStrings");

// This is true if the AppHost is being invoked as a publisher (creating deployment manifest)
// Used to determine service configuration.
var durableDevelopment = Convert.ToBoolean(builder.Configuration["ServiceConfiguration:ProjectVicoServices:DocumentGeneration:DurableDevelopmentServices"]);

// The default password for SQL server is in appsettings.json. You can override it in appsettings.Development.json.
// User name is "sa" and you must use SQL server authentication (not Azure AD/Windows authentication).
// The password should be in ServiceConfiguration:SQL:Password (see appsettings.json)
// To connect in SQL Server Management Studio to the local instance, use 127.0.0.1,9001 as the server name (NOT localhost - doesn't work!)
var sqlPassword = builder.Configuration["ServiceConfiguration:SQL:Password"];
var sqlDatabaseName = builder.Configuration["ServiceConfiguration:SQL:DatabaseName"];

// The default password for the RabbitMQ container is in appsettings.json. You can override it in appsettings.Development.json.
var rabbitMqPassword = builder.Configuration["ServiceConfiguration:RabbitMQ:Password"];

IResourceBuilder<SqlServerDatabaseResource> docGenSql;
IResourceBuilder<RabbitMQServerResource> docGenRabbitMq;
IResourceBuilder<AzureServiceBusResource>? sbus;
IResourceBuilder<IResourceWithConnectionString> queueService;



var signalr = builder.ExecutionContext.IsPublishMode
    ? builder.AddAzureSignalR("signalr")
    : builder.AddConnectionString("signalr");

if (builder.ExecutionContext.IsRunMode) // For local development
{
    if (durableDevelopment)
    {
        docGenSql = builder
            .AddSqlServer("sqldocgen", password: sqlPassword, port: 9001)
            .WithVolumeMount("pvico-sql-docgen-vol", "/var/opt/mssql")
            .AddDatabase(sqlDatabaseName);

    }
    else // Don't persist data and queue content - it will be deleted on restart!
    {
        docGenSql = builder
            .AddSqlServer("sqldocgen", password: sqlPassword, 9001)
            .AddDatabase(sqlDatabaseName);
    }

    docGenRabbitMq = builder
           .AddRabbitMQ("rabbitmqdocgen", 9002)
           //.WithAnnotation(new ContainerImageAnnotation() { Image = "rabbitmq", Tag = "3-management" })
           .WithEnvironment("NODENAME", "rabbit@localhost");

    queueService = docGenRabbitMq;
}
else // For production/Azure deployment
{
    docGenSql = builder
        .AddSqlServer("sqldocgen", password: sqlPassword, port: 9001)
        .PublishAsAzureSqlDatabase()
        .AddDatabase(sqlDatabaseName);

    sbus = builder.AddAzureServiceBus("sbus");
    queueService = sbus;
}

var apiMain = builder
    .AddProject<Projects.ProjectVico_V2_API_Main>("api-main")
    .WithHttpsEndpoint(6001)
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(signalr)
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
    .WithReference(docGenSql)
    .WithConfigSection(envServiceConfigurationConfigurationSection);

var docGenFrontend = builder
    .AddProject<Projects.ProjectVico_V2_Web_DocGen>("web-docgen")
    .WithHttpsEndpoint(5001, "httpsEndpoint")
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(signalr)
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