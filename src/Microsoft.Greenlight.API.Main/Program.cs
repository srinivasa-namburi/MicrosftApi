using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Hubs;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Orleans.Configuration;
using Orleans.Serialization;
using Scalar.AspNetCore;
using System.Reflection;

// Use standard WebApplicationBuilder instead of the custom class
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<AzureCredentialHelper>();
var credentialHelper = new AzureCredentialHelper(builder.Configuration);

// Initialize AdminHelper with configuration
AdminHelper.Initialize(builder.Configuration);

builder.AddGreenlightDbContextAndConfiguration();

var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

// Due to a bug, this MUST come before .AddServiceDefaults() (keyed services can't be present in container)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi().AddInMemoryTokenCaches();

builder.AddServiceDefaults();

//await builder.DelayStartup(serviceConfigurationOptions.GreenlightServices.DocumentGeneration.DurableDevelopmentServices);

builder.AddGreenlightServices(credentialHelper, serviceConfigurationOptions);

builder.RegisterStaticPlugins(serviceConfigurationOptions);
builder.RegisterConfiguredDocumentProcesses(serviceConfigurationOptions);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();

var entraTenantId = builder.Configuration["AzureAd:TenantId"];
var entraScopes = builder.Configuration["AzureAd:Scopes"];
var entraClientId = builder.Configuration["AzureAd:ClientId"];
var entraInstance = builder.Configuration["AzureAd:Instance"];

// If any of the required settings are missing, throw an exception and shut down

if (string.IsNullOrEmpty(entraTenantId) ||
    string.IsNullOrEmpty(entraScopes) ||
    string.IsNullOrEmpty(entraClientId) ||
    string.IsNullOrEmpty(entraInstance))
{
    throw new InvalidOperationException("Azure AD settings are missing. Please check the configuration.");
}


// Also look in the app.SwaggerUI() call at the bottom of this file
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("ms-entra", new OpenApiSecurityScheme
    {
        Description = "OAuth2.0 Auth Code with PKCE",
        Name = "oauth2",
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{entraInstance.TrimEnd('/')}/{entraTenantId}/oauth2/v2.0/authorize"),
                TokenUrl = new Uri($"{entraInstance.TrimEnd('/')}/{entraTenantId}/oauth2/v2.0/token"),
                Scopes = new Dictionary<string, string>
                {
                    { entraScopes!, "Access the API" },
                },
                // To allow Scalar to select PKCE by Default
                // valid options are 'SHA-256' | 'plain' | 'no'
                Extensions = new Dictionary<string, IOpenApiExtension>()
                {
                    ["x-usePkce"] = new OpenApiString("SHA-256")
                }

            }
        }
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ms-entra" }
            },
            new[] { entraScopes! }
        }
    });

    // Add support for text/plain
    c.MapType<string>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "text"
    });

});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});


var frontEndUrl = builder.Configuration["services:web-docgen:https:0"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder => builder.WithOrigins(frontEndUrl!)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .SetIsOriginAllowed((host) => true));
});

var useAzureSignalR = builder.Configuration["ServiceConfiguration:GreenlightServices:Scalability:UseAzureSignalR"];

if (builder.Environment.IsDevelopment() ||
    (useAzureSignalR == null || useAzureSignalR == "false") ||
    builder.Configuration.GetConnectionString("signalr") == null ||
    builder.Configuration.GetConnectionString("signalr") == string.Empty)
{
    builder.Services.AddSignalR(); // Default SignalR
}
else
{
    builder.Services.AddSignalR().AddAzureSignalR(options =>
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("signalr");
    });
}

builder.Services.AddHttpContextAccessor();
var eventHubConnectionString = builder.Configuration.GetConnectionString("greenlight-cg-streams");
var checkPointTableStorageConnectionString = builder.Configuration.GetConnectionString("checkpointing");
var orleansBlobStoreConnectionString = builder.Configuration.GetConnectionString("blob-orleans");

var currentAssembly = Assembly.GetExecutingAssembly();

//builder.AddGreenlightOrleansClient(credentialHelper);
builder.UseOrleans(siloBuilder =>
{
    siloBuilder.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = "greenlight-cluster";
        options.ServiceId = "greenlight-api-silo";
    });

    siloBuilder.Configure<SiloMessagingOptions>(options =>
    {
        options.ResponseTimeout = TimeSpan.FromMinutes(15);
        options.DropExpiredMessages = false;
    });

    siloBuilder.Configure<ClientMessagingOptions>(options =>
    {
        options.ResponseTimeout = TimeSpan.FromMinutes(15);
        options.DropExpiredMessages = false;
    });

    siloBuilder.Services.AddSerializer(serializerBuilder =>
    {
        serializerBuilder.AddJsonSerializer(DetectSerializableAssemblies);

        // Is there a way to add Orleans Serializers for referenced assemblies?
        serializerBuilder.AddAssembly(typeof(ChatMessageDTO).Assembly);
        serializerBuilder.AddAssembly(typeof(ChatMessage).Assembly);
        serializerBuilder.AddAssembly(currentAssembly);
    });

    // Add EventHub-based streaming for high throughput
    siloBuilder.AddEventHubStreams("StreamProvider", (ISiloEventHubStreamConfigurator streamsConfigurator) =>
    {
        streamsConfigurator.ConfigureEventHub(eventHubBuilder => eventHubBuilder.Configure(options =>
        {
            var eventHubNamespace = eventHubConnectionString!.Split("Endpoint=")[1].Split(":443/")[0];
            var eventHubName = eventHubConnectionString!.Split("EntityPath=")[1].Split(";")[0];
            var consumerGroup = eventHubConnectionString!.Split("ConsumerGroup=")[1].Split(";")[0];
            options.ConfigureEventHubConnection(
                eventHubNamespace,
                eventHubName,
                consumerGroup, credentialHelper.GetAzureCredential());

        }));

        streamsConfigurator.UseAzureTableCheckpointer(checkpointBuilder =>

            checkpointBuilder.Configure(options =>
            {
                options.TableServiceClient = new TableServiceClient(
                    new Uri(checkPointTableStorageConnectionString!), credentialHelper.GetAzureCredential());

                options.PersistInterval = TimeSpan.FromSeconds(10);
            }));

    });

    siloBuilder.AddAzureBlobGrainStorage("PubSubStore", options =>
    {
        var blobStorageUrl = new Uri(orleansBlobStoreConnectionString!);
        options.BlobServiceClient =
            new BlobServiceClient(blobStorageUrl, credentialHelper.GetAzureCredential());
    });

    siloBuilder.AddAzureBlobGrainStorageAsDefault(options =>
    {
        options.ContainerName = "grain-storage";
        var blobStorageUrl = new Uri(orleansBlobStoreConnectionString!);
        options.BlobServiceClient =
            new BlobServiceClient(blobStorageUrl, credentialHelper.GetAzureCredential());
    });

    siloBuilder.UseAzureTableReminderService(options =>
    {
        // Use the same table storage connection that you're using for checkpointing
        options.TableServiceClient = new TableServiceClient(
            new Uri(checkPointTableStorageConnectionString!),
            credentialHelper.GetAzureCredential());

        options.TableName = "OrleansReminders";
    });

    bool DetectSerializableAssemblies(Type arg)
    {
        // Check if the type is in any assembly starting with Microsoft.Greenlight.Grain or Microsoft.Greenlight.Shared
        // This is a bit of a hack, but it works for now
        var assemblyName = arg.Assembly.GetName().Name;
        return assemblyName != null &&
               (assemblyName.StartsWith("Microsoft.Greenlight.Grain") ||
                assemblyName.StartsWith("Microsoft.Greenlight.Shared"));
    }
});

// Bind the ServiceConfigurationOptions to configuration
builder.Services.AddOptions<ServiceConfigurationOptions>()
    .Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// This enables reloading:
builder.Services.Configure<ServiceConfigurationOptions>(
    builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));

// This sets up a background worker + all SignalR notifiers.
// We've currently disabled SignalR notifiers in the process of moving these
// to grain orchestration.
builder.Services.AddGreenlightHostedServices(addSignalrNotifiers: false);


var app = builder.Build();

var webSocketOptions = new WebSocketOptions()
{
    KeepAliveInterval = TimeSpan.FromSeconds(120)
};

webSocketOptions.AllowedOrigins.Add(frontEndUrl!);
app.UseWebSockets(webSocketOptions);

app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.UseExceptionHandler(app.Environment.IsDevelopment() ? "/error-development" : "/error");
app.UseStatusCodePages();


app.MapHub<NotificationHub>("/hubs/notification-hub");

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.MapScalarApiReference(options =>
{
    options.OpenApiRoutePattern = "/swagger/v1/swagger.json";
    options.Title = "Microsoft Greenlight API";
    options.WithPreferredScheme("ms-entra");
    options.WithOAuth2Authentication(oauth =>
    {
        oauth.ClientId = entraClientId;
        oauth.Scopes = [entraScopes];
    });

});
app.MapControllers();

app.Run();
