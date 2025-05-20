using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Greenlight.ServiceDefaults;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Management;
using Microsoft.Greenlight.Web.DocGen.Auth;
using Microsoft.Greenlight.Web.DocGen.Components;
using Microsoft.Greenlight.Web.DocGen.ServiceClients;
using Microsoft.Greenlight.Web.Shared;
using Microsoft.Greenlight.Web.Shared.Auth;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor.Services;
using StackExchange.Redis;
using System.Net.Http.Headers;
using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 1024 * 1024 * 1024; // 1024MB

});

builder.AddServiceDefaults();

// Initialize AdminHelper with configuration
AdminHelper.Initialize(builder.Configuration);

// First add the DbContext and configuration provider
builder.AddGreenlightDbContextAndConfiguration();

// Bind the ServiceConfigurationOptions to configuration
builder.Services.AddOptions<ServiceConfigurationOptions>()
    .Bind(builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// This enables reloading:
builder.Services.Configure<ServiceConfigurationOptions>(
    builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName));

var serviceConfigurationOptions = builder.Configuration.GetSection(ServiceConfigurationOptions.PropertyName).Get<ServiceConfigurationOptions>()!;

var azureAdSection = builder.Configuration.GetSection("AzureAd");
builder.Services.Configure<AzureAdOptions>(azureAdSection);
var azureAdSettings = azureAdSection.Get<AzureAdOptions>();

builder.Services.AddSingleton<AzureCredentialHelper>();
var credentialHelper = new AzureCredentialHelper(builder.Configuration);

// Register Azure blob client, Redis client and other services (unchanged)
builder.AddKeyedAzureBlobClient("blob-docing", settings =>
{
    settings.Credential = credentialHelper.GetAzureCredential();
});
builder.AddGreenLightRedisClient("redis", credentialHelper, serviceConfigurationOptions);
// Add IDistributedCache using StackExchange.Redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("redis");
});
builder.Services.AddScoped<AzureFileHelper>();

var apiUri = new Uri("https+http://api-main");

builder.Services.AddHttpClient<IAuthorizationApiClient, AuthorizationApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
});
builder.Services.AddHttpClient<IConfigurationApiClient, ConfigurationApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
});

// Authentication configuration
builder.Services.AddAuthentication("MicrosoftOidc")
    .AddOpenIdConnect("MicrosoftOidc", oidcOptions =>
    {
        oidcOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        oidcOptions.Scope.Add(OpenIdConnectScope.OfflineAccess);
        foreach (var scope in azureAdSettings.Scopes.Split(' '))
        {
            oidcOptions.Scope.Add(scope);
        }
        oidcOptions.Authority = $"{azureAdSettings.Instance.TrimEnd('/')}/{azureAdSettings.TenantId}/v2.0/";
        oidcOptions.ClientId = azureAdSettings.ClientId;
        oidcOptions.ClientSecret = azureAdSettings.ClientSecret;
        oidcOptions.ResponseType = OpenIdConnectResponseType.Code;

        oidcOptions.MapInboundClaims = false;
        oidcOptions.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.Name;
        oidcOptions.TokenValidationParameters.RoleClaimType = "role";

        oidcOptions.Events = new OpenIdConnectEvents()
        {
            OnRedirectToIdentityProvider = context =>
            {
                string externalRedirectUri = ComputeRedirectUri(context, serviceConfigurationOptions);
                context.ProtocolMessage.RedirectUri = externalRedirectUri;
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var authorizationClient = context.HttpContext.RequestServices.GetRequiredService<IAuthorizationApiClient>();
                if (!string.IsNullOrWhiteSpace(context.SecurityToken.RawData) &&
                    context.Principal.Identity is ClaimsIdentity identity &&
                    !identity.HasClaim(c => c.Type == "access_token"))
                {
                    identity.AddClaim(new Claim("access_token", context.SecurityToken.RawData));
                }
                var userInfo = UserInfo.FromClaimsPrincipal(context.Principal, context.SecurityToken);
                var user = new UserInfoDTO(userInfo.UserId, userInfo.Name)
                {
                    Email = userInfo.Email
                };
                await authorizationClient.StoreOrUpdateUserDetailsAsync(user);
            }
        };
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);

// Configure cookie options to force authentication
builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/authentication/login";
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
});

builder.Services.ConfigureOidcRefreshHandling(CookieAuthenticationDefaults.AuthenticationScheme, "MicrosoftOidc");
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddScoped<AuthenticationStateProvider, PersistingAuthenticationStateProvider>();
builder.Services.AddHttpForwarderWithServiceDiscovery();
builder.Services.AddSingleton<IUserIdProvider, SignalRCustomUserIdProvider>();

builder.Services.AddMudServices();

var useAzureSignalR = builder.Configuration["ServiceConfiguration:GreenlightServices:Scalability:UseAzureSignalR"];

if (builder.Environment.IsDevelopment() ||
    (useAzureSignalR == null || useAzureSignalR == "false") ||
    builder.Configuration.GetConnectionString("signalr") == null ||
    builder.Configuration.GetConnectionString("signalr") == string.Empty)
{
    builder.Services.AddSignalR();
}
else
{
    builder.Services.AddSignalR().AddAzureSignalR(options =>
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("signalr");
        options.ClaimsProvider = context =>
        {
            var user = context.User;
            return
            [
                new Claim("name", user.Identity?.Name ?? "unknown"),
                new Claim("preferred_username", user.FindFirst("preferred_username")?.Value ?? "unknown"),
                new Claim("access_token", user.FindFirst("access_token")?.Value ?? "unknown"),
                new Claim("sub", user.FindFirst("sub")?.Value ?? "unknown")
            ];
        };
    });
}

builder.Services.AddSingleton<DynamicComponentResolver>();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 1024 * 1024 * 1024; // 1024MB
    options.ValueLengthLimit = 1024 * 1024 * 1024;
    options.ValueCountLimit = 16384;        
});

builder.Services.AddSingleton<IHostedService, ShutdownCleanupService>();

builder.Services.AddDataProtection()
    .PersistKeysToStackExchangeRedis(() =>
    {
        var serviceProvider = builder.Services.BuildServiceProvider();
        var redisConnection = serviceProvider.GetRequiredService<IConnectionMultiplexer>();
        return redisConnection.GetDatabase();
    }, "DataProtection-Keys");

var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};

app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
else
{
    app.UseWebAssemblyDebugging();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.UseAntiforgery();
app.MapDefaultEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Microsoft.Greenlight.Web.DocGen.Client._Imports).Assembly)
    .RequireAuthorization();

// API forwarders and authentication endpoints
app.MapForwarder("/api/configuration/frontend", "https://api-main/");

app.MapForwarder("/api/{**catch-all}", "https://api-main/", transformBuilder =>
{
    transformBuilder.AddRequestTransform(async transformContext =>
    {
        var accessToken = await transformContext.HttpContext.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(accessToken))
        {
            transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
    });
}).WithMetadata(new RequestSizeLimitAttribute(1024 * 1024 * 1024))
  .RequireAuthorization();

app.MapForwarder("/hubs/{**catch-all}", "https://api-main/", transformBuilder =>
{
    transformBuilder.AddRequestTransform(async transformContext =>
    {
        var accessToken = await transformContext.HttpContext.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(accessToken))
        {
            transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
    });
}).RequireAuthorization();

app.MapGet("/api-address", () =>
{
    var apiAddress = builder.Configuration["services:api-main:https:0"];

    if (string.IsNullOrEmpty(serviceConfigurationOptions.HostNameOverride.Api))
    {
        return string.IsNullOrEmpty(apiAddress)
            ? Results.NotFound()
            : Results.Ok(apiAddress.TrimEnd('/'));
    }

    // replace the host name with the one from the configuration
    // keep the port from the original address

    var uri = new Uri(apiAddress);
    var port = uri.Port;
    var hostName = serviceConfigurationOptions.HostNameOverride.Api;
    var scheme = uri.Scheme;
    var newUri = new Uri($"{scheme}://{hostName}:{port}");
    apiAddress = newUri.ToString();
    
    return string.IsNullOrEmpty(apiAddress)
        ? Results.NotFound()
        : Results.Ok(apiAddress.TrimEnd('/'));
});

app.MapGet("/configuration/token", async context =>
{
    var accessToken = await context.Request.HttpContext.GetTokenAsync("access_token");
    if (string.IsNullOrEmpty(accessToken))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    context.Response.ContentType = "text/plain";
    await context.Response.WriteAsync(accessToken);
}).RequireAuthorization();

app.MapGroup("/authentication").MapLoginAndLogout(app);

app.Run();

string ComputeRedirectUri(RedirectContext redirectContext, ServiceConfigurationOptions? serviceConfigurationOptions1)
{
    var request = redirectContext.HttpContext.Request;
    string selectedScheme = !redirectContext.ProtocolMessage.RedirectUri.Contains("localhost") ? "https" : request.Scheme;
                
    var hostName = request.Host.ToString();
    if (!string.IsNullOrEmpty(serviceConfigurationOptions1.HostNameOverride.Web))
    {
        hostName = serviceConfigurationOptions1.HostNameOverride.Web;
        // We need to retain the port from the request in the redirect URI
        // If the hostname doesn't already have a port
        if (request.Host.Port.HasValue)
        {
            hostName = $"{hostName}:{request.Host.Port}";
        }

        // Remove duplicate ports if present (remove the last one)
        if (hostName.Contains(':'))
        {
            var parts = hostName.Split(':');
            if (parts.Length > 2)
            {
                hostName = $"{parts[0]}:{parts[1]}";
            }
        }
    }
    
    hostName = hostName.TrimEnd('/');

    var s = $"{selectedScheme}://{hostName}{request.PathBase.ToString().TrimEnd('/')}/signin-oidc";
    return s;
}
