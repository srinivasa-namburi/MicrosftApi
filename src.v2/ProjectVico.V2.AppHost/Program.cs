using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ProjectVico.V2.AppHost;


var builder = DistributedApplication.CreateBuilder(args);

var envServiceConfigurationConfigurationSection = builder.Configuration.GetSection("ServiceConfiguration");
var envAzureAdConfigurationSection = builder.Configuration.GetSection("AzureAd");
var envConnectionStringsConfigurationSection = builder.Configuration.GetSection("ConnectionStrings");

// This is true if the AppHost is being invoked as a publisher (creating deployment manifest)
// Used to determine service configuration.
bool azdDeploy = args.Contains("--publisher");

var durableDevelopment = Convert.ToBoolean(builder.Configuration["ServiceConfiguration:ProjectVicoServices:DocumentGeneration:DurableDevelopmentServices"]);

// The default password for SQL server is in appsettings.json. You can override it in appsettings.Development.json.
// User name is "sa" and you must use SQL server authentication (not Azure AD/Windows authentication).
// The password should be in ServiceConfiguration:SQL:Password (see appsettings.json)
// To connect in SQL Server Management Studio to the local instance, use 127.0.0.1,9001 as the server name (NOT localhost - doesn't work!)
var sqlPassword = builder.Configuration["ServiceConfiguration:SQL:Password"];

// The default password for the RabbitMQ container is in appsettings.json. You can override it in appsettings.Development.json.
var rabbitMqPassword = builder.Configuration["ServiceConfiguration:RabbitMQ:Password"];

IResourceBuilder<SqlServerDatabaseResource> docGenSqlLocal = null;
IResourceBuilder<RabbitMQContainerResource> docGenRabbitMq = null;

IResourceBuilder<AzureSqlDatabaseResource> docGenSqlAzure = null;
IResourceBuilder<AzureServiceBusResource> serviceBus=null;

var docIngBlobs = builder
    .AddAzureStorage("storage-docing")
    //.UseEmulator()
    .AddBlobs("blob-docing");

if (!azdDeploy) // For local development
{
    if (durableDevelopment)
    {
        docGenSqlLocal = builder
            .AddSqlServerContainer("sql-docgen", password: sqlPassword, 9001)
            .WithVolumeMount("sql-docgen-vol", "/var/opt/mssql", VolumeMountType.Named)
            .AddDatabase("ProjectVICOdb");


        docGenRabbitMq = builder
            .AddRabbitMQContainer("rabbitmq-docgen", 9002, password: rabbitMqPassword)
            .WithAnnotation(new ContainerImageAnnotation() { Image = "rabbitmq", Tag = "3-management" })
            .WithEnvironment("NODENAME", "rabbit@localhost")
            .WithVolumeMount("rabbitmq-docgen-vol", "/var/lib/rabbitmq", VolumeMountType.Named);
    }
    else // Don't persist data and queue content - it will be deleted on restart!
    {
        docGenSqlLocal = builder
            .AddSqlServerContainer("sql-docgen", password: sqlPassword, 9001)
            .AddDatabase("ProjectVICOdb");

        docGenRabbitMq = builder
            .AddRabbitMQContainer("rabbitmq-docgen", 9002, password: rabbitMqPassword);

    }
}
else // For production/Azure deployment
{
    docGenSqlAzure = builder
        .AddAzureSqlServer("sql-docgen-server")
        .AddDatabase("ProjectVICOdb");

    serviceBus = builder
        .AddAzureServiceBus("sbus");
}

var pluginGeographicalData = builder
    .AddProject<Projects.ProjectVico_V2_Plugins_GeographicalData>("plugin-geographicaldata")
    .WithHttpsEndpoint(7001)
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    ;

var pluginEarthquake = builder
    .AddProject<Projects.ProjectVico_V2_Plugins_Earthquake>("plugin-earthquake")
    .WithHttpsEndpoint(7002)
    .WithConfigSection(envAzureAdConfigurationSection)
    ;

var pluginNuclearDocs = builder
    .AddProject<Projects.ProjectVico_V2_Plugins_NuclearDocs>("plugin-nucleardocs")
    .WithHttpsEndpoint(7003)
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    ;

var apiMain = builder
    .AddProject<Projects.ProjectVico_V2_API_Main>("api-main")
    .WithHttpsEndpoint(6001)
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithReference(docIngBlobs)
    .WithReference(pluginGeographicalData)
    .WithReference(pluginEarthquake);

if (!azdDeploy)
{
    apiMain
        .WithReference(docGenSqlLocal)
        .WithReference(docGenRabbitMq);
}
else
{
    apiMain
        .WithReference(docGenSqlAzure)
        .WithReference(serviceBus);
}

var workerDocumentGeneration = builder
    .AddProject<Projects.ProjectVico_V2_Worker_DocumentGeneration>("worker-documentgeneration")
    .WithReplicas(Convert.ToUInt16(
        builder.Configuration["ServiceConfiguration:ProjectVicoServices:DocumentGeneration:NumberOfGenerationWorkers"]))
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(apiMain);


var workerDocumentIngestion = builder
    .AddProject<Projects.ProjectVico_V2_Worker_DocumentIngestion>("worker-documentingestion")
    .WithReplicas(Convert.ToUInt16(
        builder.Configuration["ServiceConfiguration:ProjectVicoServices:DocumentIngestion:NumberOfIngestionWorkers"]))
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithReference(docIngBlobs)
    .WithReference(apiMain);

var setupManager = builder
    .AddProject<Projects.ProjectVico_V2_SetupManager>("worker-setupmanager")
    .WithConfigSection(envServiceConfigurationConfigurationSection);


if (!azdDeploy)
{
    workerDocumentGeneration
        .WithReference(docGenSqlLocal!)
        .WithReference(docGenRabbitMq!);

    workerDocumentIngestion
        .WithReference(docGenSqlLocal!)
        .WithReference(docGenRabbitMq!);

    setupManager
        .WithReference(docGenSqlLocal!);

}
else
{
    workerDocumentGeneration
        .WithReference(docGenSqlAzure!)
        .WithReference(serviceBus!);

    workerDocumentIngestion
        .WithReference(docGenSqlAzure!)
        .WithReference(serviceBus!);

    setupManager
        .WithReference(docGenSqlAzure!);
}

var docGenFrontend = builder
    .AddProject<Projects.ProjectVico_V2_Web_DocGen>("web-docgen")
    .WithHttpsEndpoint(5001, "httpsEndpoint")
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithReference(apiMain)
    .WithReference(docIngBlobs)
    ;

    

builder.Build().Run();