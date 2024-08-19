using Azure.Identity;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Identity.Web;
using ProjectVico.V2.API.Main.Hubs;
using ProjectVico.V2.DocumentProcess.Shared;
using ProjectVico.V2.Plugins.Shared;
using ProjectVico.V2.Shared;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Extensions;
using ProjectVico.V2.Shared.Mappings;
using ProjectVico.V2.Shared.Services;
using ProjectVico.V2.Shared.Services.Search;

var builder = WebApplication.CreateBuilder(args);

// Due to a bug, this MUST come before .AddServiceDefaults() (keyed services can't be present in container)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi().AddInMemoryTokenCaches();

builder.AddServiceDefaults();

// Set Configuration ServiceConfigurationOptions:CognitiveServices:Endpoint to the correct value
// Read the values from the azureAiSearch Connection String
// Build an IConfigurationSection from ServiceConfigurationOptions, but set the ConnectionString to the azureAiSearch Connection String
builder.AddAzureSearchClient("aiSearch");
builder.Services.AddOptions<ServiceConfigurationOptions>().Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));
builder.Services.AddSingleton<SearchClientFactory>();

var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

await builder.DelayStartup(serviceConfigurationOptions.ProjectVicoServices.DocumentGeneration.DurableDevelopmentServices);

builder.AddAzureServiceBusClient("sbus");
builder.AddRabbitMQClient("rabbitmqdocgen");
builder.AddKeyedAzureOpenAIClient("openai-planner");
builder.AddAzureBlobClient("blob-docing");
builder.AddRedisClient("redis");


builder.AddDocGenDbContext(serviceConfigurationOptions);

builder.Services.AddAutoMapper(typeof(ChatMessageProfile));

builder.DynamicallyRegisterPlugins(serviceConfigurationOptions);
builder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);
builder.AddSemanticKernelServices(serviceConfigurationOptions);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbitMqConnectionString);
            cfg.ConfigureEndpoints(context);
        });
    });
}

var frontEndUrl = builder.Configuration["services:web-docgen:https:0"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder => builder.WithOrigins(frontEndUrl)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .SetIsOriginAllowed((host) => true));
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSignalR();
}
else
{
    builder.Services.AddSignalR().AddAzureSignalR(options =>
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("signalr");
    });
}

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

var webSocketOptions = new WebSocketOptions()
{
    KeepAliveInterval = TimeSpan.FromSeconds(120)
};

webSocketOptions.AllowedOrigins.Add(frontEndUrl);
app.UseWebSockets(webSocketOptions);

app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.UseExceptionHandler(app.Environment.IsDevelopment() ? "/error-development" : "/error");
app.UseStatusCodePages();

app.MapHub<NotificationHub>("/hubs/notification-hub", options =>
{

});

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();
