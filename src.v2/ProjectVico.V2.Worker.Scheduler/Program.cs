using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Search.Documents;
using Azure;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using ProjectVico.V2.Shared.Classification.Classifiers;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Interfaces;
using ProjectVico.V2.Shared.Mappings;
using ProjectVico.V2.Shared.Pipelines;
using ProjectVico.V2.Shared.SagaState;
using ProjectVico.V2.Shared.Search;
using ProjectVico.V2.Worker.Scheduler;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOptions<ServiceConfigurationOptions>().Bind(builder.Configuration.GetSection("ServiceConfiguration"));
var serviceConfigurationOptions = builder.Configuration.GetSection("ServiceConfiguration").Get<ServiceConfigurationOptions>()!;

// Common services and dependencies
builder.AddAzureServiceBus("sbus");
builder.AddRabbitMQ("rabbitmq-docgen");
builder.AddAzureBlobService("blob-docing");

// Ingestion specific custom dependencies
builder.Services.AddScoped<AzureFileHelper>();

builder.AddSqlServerDbContext<DocGenerationDbContext>("sql-docgen", settings =>
{
    settings.ConnectionString = builder.Configuration.GetConnectionString("ProjectVICOdb");
    settings.DbContextPooling = true;
    settings.HealthChecks = true;
    settings.Tracing = true;
    settings.Metrics = true;
});

var serviceBusConnectionString = builder.Configuration.GetConnectionString("sbus");
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq-docgen");

if (!builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(serviceBusConnectionString))
{
    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        x.AddConsumers(typeof(Program).Assembly);
        
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

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.PrefetchCount = 1;
            cfg.ConcurrentMessageLimit = 1;
            cfg.Host(rabbitMqConnectionString);
            cfg.ConfigureEndpoints(context);
        });
    });
}

builder.Services.AddHostedService<ScheduledBlobAutoImportWorker>();

var host = builder.Build();
host.Run();
