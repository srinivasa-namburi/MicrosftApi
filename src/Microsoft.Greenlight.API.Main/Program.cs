using Azure.Data.Tables;
using Azure.Storage.Blobs;
using LazyCache;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Enums;
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

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 1024 * 1024 * 1024; // 1024MB
});

builder.Services.AddSingleton<AzureCredentialHelper>();
var credentialHelper = new AzureCredentialHelper(builder.Configuration);

// Initialize AdminHelper with configuration and validate developer setup
AdminHelper.Initialize(builder.Configuration);
AdminHelper.ValidateDeveloperSetup("API Main");

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

// Security services
builder.Services.AddSingleton<Microsoft.Greenlight.Shared.Services.Security.ISecretHashingService, Microsoft.Greenlight.Shared.Services.Security.SecretHashingService>();

// Configure caching for authorization (LazyCache with hybrid local+remote capabilities)
builder.Services.AddSingleton<IAppCache, CachingService>();

// Authorization policies based on dynamic permissions
builder.Services.AddSingleton<IAuthorizationPolicyProvider, Microsoft.Greenlight.API.Main.Authorization.PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.Greenlight.API.Main.Authorization.ICachedPermissionService, Microsoft.Greenlight.API.Main.Authorization.CachedPermissionService>();
builder.Services.AddScoped<IAuthorizationHandler, Microsoft.Greenlight.API.Main.Authorization.RequiresPermissionHandler>();
builder.Services.AddScoped<IAuthorizationHandler, Microsoft.Greenlight.API.Main.Authorization.RequiresAnyPermissionHandler>();
builder.Services.AddScoped<Microsoft.Greenlight.Shared.Authorization.AuthorizationProtectionService>();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024 * 1024 * 1024; // 1024MB
    options.ValueLengthLimit = 1024 * 1024 * 1024;         // 1024MB
    options.ValueCountLimit = 16384;                       // Increased if needed
});

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


var frontEndUrl = AdminHelper.GetWebDocGenServiceUrl();

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder => builder.WithOrigins(frontEndUrl!)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .SetIsOriginAllowed((host) => true));
});

// SignalR Hosting Strategy:
// - API self-hosts SignalR with Redis backplane for scale-out
// - Azure SignalR is deprecated due to reliability issues
// - Redis provides reliable message delivery and better control
// This ensures unified endpoint exposure through Application Gateway at /hubs/*

var useAzureSignalR = builder.Configuration["ServiceConfiguration:GreenlightServices:Scalability:UseAzureSignalR"];
var enableAzureSignalR = useAzureSignalR != null && useAzureSignalR.Equals("true", StringComparison.OrdinalIgnoreCase);

if (enableAzureSignalR)
{
    Console.WriteLine("WARNING: Azure SignalR is deprecated and should not be used. Falling back to Redis backplane.");
}

// Always self-host SignalR with Redis backplane for reliability
var signalRBuilder = builder.Services.AddSignalR(options =>
{
    // Configure for Application Gateway and custom domain scenarios
    options.EnableDetailedErrors = !builder.Environment.IsProduction();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.MaximumReceiveMessageSize = 102400; // 100KB
});

// Configure Redis connections
var redisConnectionString = builder.Configuration.GetConnectionString("redis");
var redisSignalRConnectionString = builder.Configuration.GetConnectionString("redis-signalr");

// Use redis-signalr for SignalR backplane if available, fallback to main redis
var signalRRedisConnection = redisSignalRConnectionString ?? redisConnectionString;

if (!string.IsNullOrEmpty(signalRRedisConnection))
{
    signalRBuilder.AddStackExchangeRedis(signalRRedisConnection, options =>
    {
        options.Configuration.AbortOnConnectFail = false; // Don't crash on Redis connection issues
    });
    Console.WriteLine($"SignalR: Self-hosted with Redis backplane (using {(redisSignalRConnectionString != null ? "redis-signalr" : "redis")})");
}
else
{
    Console.WriteLine("SignalR: Self-hosted without Redis backplane (single instance mode)");
}

// Configure Data Protection using main Redis instance
if (!string.IsNullOrEmpty(redisConnectionString))
{
    // Configure Data Protection to use Redis for key storage
    // This ensures encryption keys are shared across all API instances
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "greenlight-api";
    });

    builder.Services.AddDataProtection()
        .PersistKeysToStackExchangeRedis(
            StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString),
            "DataProtection-Keys")
        .SetApplicationName("greenlight-api");

    Console.WriteLine("DataProtection: Using Redis for key storage");
}
else
{
    Console.WriteLine("DataProtection: Using local file system (not recommended for production)");
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

    // Use Redis streams for local development, EventHub for production
    if (AdminHelper.IsRunningInProduction())
    {
        // Add EventHub-based streaming for high throughput in production
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
                    var tuple = Microsoft.Greenlight.Shared.Helpers.AzureStorageHelper.ParseTableEndpointAndCredential(checkPointTableStorageConnectionString!);
                    var tableEndpoint = tuple.endpoint;
                    var sharedKey = tuple.sharedKeyCredential;
                    if (sharedKey != null)
                    {
                        options.TableServiceClient = new TableServiceClient(tableEndpoint, sharedKey);
                    }
                    else
                    {
                        options.TableServiceClient = new TableServiceClient(new Uri(checkPointTableStorageConnectionString!), credentialHelper.GetAzureCredential());
                    }
                    options.PersistInterval = TimeSpan.FromSeconds(10);
                }));

        });
    }
    else
    {
        // Use memory streams with Redis PubSub store for local development
        // This enables stream sharing between hosts while using Orleans built-in infrastructure
        siloBuilder.AddMemoryStreams("StreamProvider");
    }

    // Configure PubSubStore - use Redis for local dev to enable stream sharing
    if (AdminHelper.IsRunningInProduction())
    {
        siloBuilder.AddAzureBlobGrainStorage("PubSubStore", options =>
        {
            var tuple = AzureStorageHelper.ParseBlobEndpointAndCredential(orleansBlobStoreConnectionString!);
            var blobEndpoint = tuple.endpoint;
            var sharedKey = tuple.sharedKeyCredential;
            if (sharedKey != null)
            {
                options.BlobServiceClient = new BlobServiceClient(blobEndpoint, sharedKey);
            }
            else
            {
                options.BlobServiceClient = new BlobServiceClient(new Uri(orleansBlobStoreConnectionString!), credentialHelper.GetAzureCredential());
            }
        });
    }
    else
    {
        // Use Redis storage for PubSub in local development to enable stream sharing
        var redisConnectionString = builder.Configuration.GetConnectionString("redis");
        siloBuilder.AddRedisGrainStorage("PubSubStore", options =>
        {
            options.ConfigurationOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnectionString!);
        });
    }

    siloBuilder.AddAzureBlobGrainStorageAsDefault(options =>
    {
        options.ContainerName = "grain-storage";
        var tuple = AzureStorageHelper.ParseBlobEndpointAndCredential(orleansBlobStoreConnectionString!);
        var blobEndpoint = tuple.endpoint;
        var sharedKey = tuple.sharedKeyCredential;
        if (sharedKey != null)
        {
            options.BlobServiceClient = new BlobServiceClient(blobEndpoint, sharedKey);
        }
        else
        {
            options.BlobServiceClient = new BlobServiceClient(new Uri(orleansBlobStoreConnectionString!), credentialHelper.GetAzureCredential());
        }
    });

    // Configure clustering using Azure Table Storage - same table as other Orleans services
    var clusteringConnectionString = builder.Configuration.GetConnectionString("clustering");
    if (!string.IsNullOrWhiteSpace(clusteringConnectionString))
    {
        siloBuilder.UseAzureStorageClustering(options =>
        {
            var tuple = AzureStorageHelper.ParseTableEndpointAndCredential(clusteringConnectionString!);
            var tableEndpoint = tuple.endpoint;
            var sharedKey = tuple.sharedKeyCredential;

            if (sharedKey != null)
            {
                options.TableServiceClient = new TableServiceClient(tableEndpoint, sharedKey);
            }
            else if (clusteringConnectionString.Contains('=')
                     || clusteringConnectionString.StartsWith("UseDevelopmentStorage", StringComparison.OrdinalIgnoreCase))
            {
                options.TableServiceClient = new TableServiceClient(clusteringConnectionString);
            }
            else if (Uri.TryCreate(clusteringConnectionString, UriKind.Absolute, out var endpointUri))
            {
                options.TableServiceClient = new TableServiceClient(endpointUri, credentialHelper.GetAzureCredential());
            }
            else
            {
                options.TableServiceClient = new TableServiceClient(clusteringConnectionString);
            }

            options.TableName = "OrleansSiloInstances"; // must match other Orleans services' clustering table name
        });
    }

    siloBuilder.UseAzureTableReminderService(options =>
    {
        var tuple = AzureStorageHelper.ParseTableEndpointAndCredential(checkPointTableStorageConnectionString!);
        var tableEndpoint = tuple.endpoint;
        var sharedKey = tuple.sharedKeyCredential;
        if (sharedKey != null)
        {
            options.TableServiceClient = new TableServiceClient(tableEndpoint, sharedKey);
        }
        else
        {
            options.TableServiceClient = new TableServiceClient(new Uri(checkPointTableStorageConnectionString!), credentialHelper.GetAzureCredential());
        }
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
    .PostConfigure(o =>
    {
        // Derive (not user configurable) vector store type from UsePostgresMemory flag
        o.GreenlightServices.VectorStore.StoreType = o.GreenlightServices.Global.UsePostgresMemory
            ? VectorStoreType.PostgreSQL
            : VectorStoreType.AzureAISearch;
    })
    .ValidateDataAnnotations()
    .ValidateOnStart();

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
