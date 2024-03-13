using Azure.Identity;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Identity.Web;
using ProjectVico.V2.API.Main.Hubs;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;

var builder = WebApplication.CreateBuilder(args);

// Due to a bug, this MUST come before .AddServiceDefaults() (keyed services can't be present in container)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi().AddInMemoryTokenCaches();

builder.AddServiceDefaults();

var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.AddAzureServiceBus("sbus");
builder.AddRabbitMQ("rabbitmqdocgen");
builder.AddAzureBlobService("docGenBlobs");

builder.AddSqlServerDbContext<DocGenerationDbContext>("sqldocgen", settings =>
{
    settings.ConnectionString = builder.Configuration.GetConnectionString(serviceConfigurationOptions.SQL.DatabaseName);
    settings.HealthChecks = true;
    settings.Tracing = true;
    settings.Metrics = true;
});

builder.Services.AddHttpContextAccessor();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var serviceBusConnectionString = builder.Configuration.GetConnectionString("sbus");
serviceBusConnectionString = serviceBusConnectionString?.Replace("https://", "sb://").Replace(":443/", "/");
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmqdocgen");

if (!string.IsNullOrWhiteSpace(serviceBusConnectionString)) // Use Azure Service Bus for production
{
    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        x.AddConsumers(typeof(Program).Assembly);
        x.UsingAzureServiceBus((context, cfg) =>
        {
            cfg.Host(serviceBusConnectionString, configure: config =>
            {
                config.TokenCredential = new DefaultAzureCredential();
            });
            cfg.ConfigureEndpoints(context);

        });
    });
}
else // Use RabbitMQ for local development
{
    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        x.AddConsumers(typeof(Program).Assembly);

        // This is to ensure RabbitMQ has enough time to start up before MassTransit tries to connect to it

        x.UsingRabbitMq((context, cfg) =>
        {
            Thread.Sleep(7000);
            cfg.Host(rabbitMqConnectionString);
            cfg.ConfigureEndpoints(context);
        });
    });
}

builder.Services.AddSignalR();


var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.UseExceptionHandler(app.Environment.IsDevelopment() ? "/error-development" : "/error");
app.UseStatusCodePages();

app.MapHub<NotificationHub>("/hubs/notification-hub");

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();
