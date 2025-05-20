using Aspire.Hosting.Orleans;
using Microsoft.Extensions.Configuration;
using Microsoft.Greenlight.AppHost;


var builder = DistributedApplication.CreateBuilder(args);
AppHostConfigurationSetup(builder);

var envServiceConfigurationConfigurationSection = builder.Configuration.GetSection("ServiceConfiguration");
var envAzureAdConfigurationSection = builder.Configuration.GetSection("AzureAd");
var envConnectionStringsConfigurationSection = builder.Configuration.GetSection("ConnectionStrings");
var envAzureConfigurationSection = builder.Configuration.GetSection("Azure");
var envKestrelConfigurationSection = builder.Configuration.GetSection("Kestrel");

// Change from ADO
var sqlPassword = builder.AddParameter("sqlPassword", true);
var sqlDatabaseName = builder.Configuration["ServiceConfiguration:SQL:DatabaseName"];

IResourceBuilder<IResourceWithConnectionString> docGenSql;
IResourceBuilder<IResourceWithConnectionString> redisResource;
IResourceBuilder<IResourceWithConnectionString> signalr;
IResourceBuilder<IResourceWithConnectionString> azureAiSearch;
IResourceBuilder<IResourceWithConnectionString> blobStorage;
IResourceBuilder<IResourceWithConnectionString> insights = null!;
IResourceBuilder<IResourceWithConnectionString> eventHub;
IResourceBuilder<IResourceWithConnectionString> orleansClusteringTable;
IResourceBuilder<IResourceWithConnectionString> orleansBlobStorage;
IResourceBuilder<IResourceWithConnectionString> orleansCheckpointing;
IResourceBuilder<IResourceWithConnectionString> kmvectorDb;

if (builder.ExecutionContext.IsRunMode) // For local development
{
    redisResource = builder.AddRedis("redis", 16379);

    // Use Azure SQL Server for local development.
    // Especially useful for ARM/AMD based machines that can't run SQL Server in a container
    var useAzureSqlServer = Convert.ToBoolean(
        builder.Configuration["ServiceConfiguration:GreenlightServices:Global:UseAzureSqlServer"]);

    if (useAzureSqlServer)
    {
        docGenSql = builder
         .AddAzureSqlServer("sqldocgen")
         .AddDatabase(sqlDatabaseName!)
         ;
    }
    else
    {
        docGenSql = builder
        .AddSqlServer("sqldocgen", password: sqlPassword, port: 9001)
        .WithDataVolume("pvico-sql-docgen-vol")
        .WithLifetime(ContainerLifetime.Persistent)
        .AddDatabase(sqlDatabaseName!);
    }

    // Test for each connection string. If it's not available, use the buidler.AddAzure... for each 
    // resource type.
    signalr = builder.Configuration.GetConnectionString("signalr") != null
        ? builder.AddConnectionString("signalr")
        : builder.AddAzureSignalR("signalr");

    eventHub = builder.Configuration.GetConnectionString("greenlight-cg-streams") != null
        ? builder.AddConnectionString("greenlight-cg-streams")
        : builder.AddAzureEventHubs("eventhub")
            .AddHub("greenlight-hub")
            .AddConsumerGroup("greenlight-cg-streams");

    azureAiSearch = builder.Configuration.GetConnectionString("aiSearch") != null
        ? builder.AddConnectionString("aiSearch")
        : builder.AddAzureSearch("aiSearch");

    blobStorage = builder.Configuration.GetConnectionString("blob-docing") != null
        ? builder.AddConnectionString("blob-docing")
        : builder
            .AddAzureStorage("docing")
            .AddBlobs("blob-docing");

    // Only add Application Insights if the connection string is set
    if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        insights = builder.AddConnectionString("insights", "APPLICATIONINSIGHTS_CONNECTION_STRING");
    else
    {
        var useAppInsights = Convert.ToBoolean(
            builder.Configuration["ServiceConfiguration:GreenlightServices:Global:UseApplicationInsights"]);
        if (useAppInsights)
            insights = builder.AddAzureApplicationInsights("insights");
    }


    var orleansStorage = builder
        .AddAzureStorage("orleans-storage").RunAsEmulator();

    orleansBlobStorage = orleansStorage
        .AddBlobs("blob-orleans");

    orleansClusteringTable = orleansStorage
        .AddTables("clustering");

    orleansCheckpointing = orleansStorage
        .AddTables("checkpointing");

    kmvectorDb = builder.AddPostgres("kmvectordb-server", port: 9002)
        .WithImage("pgvector/pgvector:pg16") // Adds pgvector support to Postgres by using a custom image
        .WithDataVolume("pvico-pgsql-kmvectordb-vol")
        .WithLifetime(ContainerLifetime.Persistent)
        .AddDatabase("kmvectordb");

}
else // For production/Azure deployment
{
    docGenSql = builder.AddAzureSqlServer("sqldocgen").AddDatabase(sqlDatabaseName!);

    redisResource = builder.AddAzureRedis("redis");
    azureAiSearch = builder.AddAzureSearch("aiSearch");
    signalr = builder.AddAzureSignalR("signalr");

    blobStorage = builder.AddAzureStorage("docing")
        .AddBlobs("blob-docing");

    insights = builder.AddAzureApplicationInsights("insights");

    eventHub = builder.AddAzureEventHubs("eventhub")
        .AddHub("greenlight-hub")
        .AddConsumerGroup("greenlight-cg-streams");

    var orleansStorage = builder.AddAzureStorage("orleans-storage");

    orleansBlobStorage = orleansStorage
        .AddBlobs("blob-orleans");

    orleansClusteringTable = orleansStorage
        .AddTables("clustering");

    orleansCheckpointing = orleansStorage
        .AddTables("checkpointing");

    kmvectorDb = builder.AddAzurePostgresFlexibleServer("kmvectordb-server")
        .AddDatabase("kmvectordb");
}

OrleansService orleans = builder.AddOrleans("default")
    .WithClustering(orleansClusteringTable)
    .WithClusterId("greenlight-cluster")
    .WithServiceId("greenlight-main-silo")
    .WithGrainStorage(orleansBlobStorage);

var dbSetupManager = builder
    .AddProject<Projects.Microsoft_Greenlight_SetupManager_DB>("db-setupmanager")
    .WithReplicas(1) // There can only be one Setup Manager
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithConfigSection(envAzureConfigurationSection)
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithReference(docGenSql)
    .WithReference(redisResource)
    .WithReference(kmvectorDb)
    .WaitFor(docGenSql) // We need this to be up and running before we can run the setup manager
    .WaitFor(redisResource); // Wait for this to make it ready for other services
 
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
    .WithReference(kmvectorDb)
    .WithReference(azureAiSearch)
    .WithReference(eventHub)
    .WithReference(orleans.AsClient())
    .WithReference(orleansCheckpointing)
    .WithReference(orleansBlobStorage)
    .WithReference(orleansClusteringTable)
    .WaitForCompletion(dbSetupManager);

var silo = builder.AddProject<Projects.Microsoft_Greenlight_Silo>("silo")
    .WithReplicas(1)
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithConfigSection(envAzureConfigurationSection)
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithReference(blobStorage)
    .WithReference(docGenSql)
    .WithReference(kmvectorDb)
    .WithReference(redisResource)
    .WithReference(azureAiSearch)
    .WithReference(eventHub)
    .WithReference(orleans)
    .WithReference(orleansCheckpointing)
    .WithReference(orleansBlobStorage)
    .WithReference(orleansClusteringTable)
    .WithReference(apiMain)
    .WaitForCompletion(dbSetupManager);

if (builder.ExecutionContext.IsRunMode)
{
    // Fix the ports on the Orleans silo for development to avoid
    // gateway and silo port reshuffle which delays startup
    silo.WithEnvironment("Orleans__Endpoints__GatewayPort", "10090")
        .WithEnvironment("Orleans__Endpoints__SiloPort", "10091");
}

var docGenFrontend = builder
    .AddProject<Projects.Microsoft_Greenlight_Web_DocGen>("web-docgen")
    .WithExternalHttpEndpoints()
    .WithConfigSection(envAzureAdConfigurationSection)
    .WithConfigSection(envServiceConfigurationConfigurationSection)
    .WithConfigSection(envConnectionStringsConfigurationSection)
    .WithConfigSection(envAzureConfigurationSection)
    .WithConfigSection(envKestrelConfigurationSection)
    .WithReference(azureAiSearch)
    .WithReference(blobStorage)
    .WithReference(docGenSql)
    .WithReference(redisResource)
    .WithReference(signalr)
    .WithReference(eventHub)
    .WithReference(apiMain)
    .WithReference(orleans.AsClient())
    .WithReference(orleansCheckpointing)
    .WithReference(orleansBlobStorage)
    .WithReference(orleansClusteringTable)
    .WaitFor(apiMain)
    .WaitForCompletion(dbSetupManager);

apiMain.WithReference(docGenFrontend); // Necessary for CORS policy creation

if (insights is not null)
{
    apiMain.WithReference(insights);
    silo.WithReference(insights);
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
