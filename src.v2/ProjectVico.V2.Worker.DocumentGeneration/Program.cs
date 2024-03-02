using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.SagaState;
using ProjectVico.V2.Worker.DocumentGeneration.AI;
using ProjectVico.V2.Worker.DocumentGeneration.Sagas;
using ProjectVico.V2.Worker.DocumentGeneration.Services;


var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOptions<ServiceConfigurationOptions>().Bind(builder.Configuration.GetSection("ServiceConfiguration"));
var serviceConfigurationOptions = builder.Configuration.GetSection("ServiceConfiguration").Get<ServiceConfigurationOptions>()!;

if (serviceConfigurationOptions.ProjectVicoServices.DocumentGeneration.CreateBodyTextNodes)
{
    builder.Services.AddSingleton<IBodyTextGenerator, SemanticKernelBodyTextGenerator>();
}
else
{
    builder.Services.AddSingleton<IBodyTextGenerator, LoremIpsumBodyTextGenerator>();
}

builder.AddAzureServiceBus("sbus");
builder.AddRabbitMQ("rabbitmq-docgen");

builder.AddKeyedAzureOpenAI("openai-planner");

builder.AddAzureBlobService("docGenBlobs");

builder.AddSqlServerDbContext<DocGenerationDbContext>("sql-docgen", settings =>
{
    settings.ConnectionString = builder.Configuration.GetConnectionString("ProjectVICOdb");
    settings.DbContextPooling = true;
    settings.HealthChecks = true;
    settings.Tracing = true;
    settings.Metrics = true;
});


builder.AddSemanticKernelService();

var serviceBusConnectionString = builder.Configuration.GetConnectionString("sbus");
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq-docgen");

if (!builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(serviceBusConnectionString))
{
    builder.Services.AddMassTransit(x =>
     {
         x.SetKebabCaseEndpointNameFormatter();
         x.AddConsumers(typeof(Program).Assembly);

         x.AddSagaStateMachine<DocumentGenerationSaga, DocumentGenerationSagaState>()
             .EntityFrameworkRepository(cfg =>
             {
                 cfg.ExistingDbContext<DocGenerationDbContext>();
                 cfg.LockStatementProvider =
                     new SqlLockStatementProvider("dbo", new SqlServerLockStatementFormatter(true));
             });

         x.UsingAzureServiceBus((context, cfg) =>
         {
             cfg.Host(serviceBusConnectionString);
             cfg.ConfigureEndpoints(context);
             cfg.ConcurrentMessageLimit = 1;
             cfg.PrefetchCount = 3;
         });
     });
}
else
{
    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        x.AddConsumers(typeof(Program).Assembly);

        x.AddSagaStateMachine<DocumentGenerationSaga, DocumentGenerationSagaState>()
            .EntityFrameworkRepository(cfg =>
            {
                cfg.ExistingDbContext<DocGenerationDbContext>();
                cfg.LockStatementProvider =
                    new SqlLockStatementProvider("dbo", new SqlServerLockStatementFormatter(true));
            });

        // This is to ensure RabbitMQ has enough time to start up before MassTransit tries to connect to it
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.PrefetchCount = 1;
            cfg.ConcurrentMessageLimit = 1;
            cfg.Host(rabbitMqConnectionString);
            cfg.ConfigureEndpoints(context);
        });
    });
}

//builder.Services.AddHostedService<DemoWorker>();

var host = builder.Build();

// Run the DbContext EnsureCreated and EnsureDeleted methods
var services = host.Services;

host.Run();
