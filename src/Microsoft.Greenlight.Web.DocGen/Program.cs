using MassTransit;
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
using Microsoft.Greenlight.Shared.Core;
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
using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;

var builder = new GreenlightDynamicWebApplicationBuilder(args);

builder.AddServiceDefaults();

//Initialize AdminHelper with configuration
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

builder.AddGreenlightServices(credentialHelper, serviceConfigurationOptions);

var apiUri = new Uri("https+http://api-main");

builder.Services.AddHttpClient<IDocumentGenerationApiClient, DocumentGenerationApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
});
builder.Services.AddHttpClient<IDocumentIngestionApiClient, DocumentIngestionApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
});
builder.Services.AddHttpClient<IContentNodeApiClient, ContentNodeApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
});
builder.Services.AddHttpClient<IChatApiClient, ChatApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
});
builder.Services.AddHttpClient<IAuthorizationApiClient, AuthorizationApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
});
builder.Services.AddHttpClient<IConfigurationApiClient, ConfigurationApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
});
builder.Services.AddHttpClient<IDocumentProcessApiClient, DocumentProcessApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
});
builder.Services.AddHttpClient<IDocumentOutlineApiClient, DocumentOutlineApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
});
builder.Services.AddHttpClient<IReviewApiClient, ReviewApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.MaxRequestContentBufferSize = 10 * 1024 * 1024 * 20; // 200 MB
    return handler;
});
builder.Services.AddHttpClient<IPluginApiClient, PluginApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
});
builder.Services.AddHttpClient<IDocumentLibraryApiClient, DocumentLibraryApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
});
builder.Services.AddHttpClient<IFileApiClient, FileApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.MaxRequestContentBufferSize = 10 * 1024 * 1024 * 20; // 200 MB
    return handler;
});

builder.Services.AddHttpClient<IDomainGroupsApiClient, DomainGroupsApiClient>(httpClient =>
{
    httpClient.BaseAddress = apiUri;
});

// Add services to the container.
builder.Services.AddAuthentication("MicrosoftOidc")
    .AddOpenIdConnect("MicrosoftOidc", oidcOptions =>
    {
        //oidcOptions.NonceCookie.SameSite = SameSiteMode.None;
        //oidcOptions.CorrelationCookie.SameSite = SameSiteMode.None;
        oidcOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

        oidcOptions.Scope.Add(OpenIdConnectScope.OfflineAccess);
        foreach (var scope in azureAdSettings.Scopes.Split(' '))
        {
            oidcOptions.Scope.Add(scope);
        }

        // ........................................................................
        // The following example Authority is configured for Microsoft Entra ID
        // and a single-tenant application registration. Set the {TENANT ID} 
        // placeholder to the Tenant ID. The "common" Authority 
        // https://login.microsoftonline.com/common/v2.0/ should be used 
        // for multi-tenant apps. You can also use the "common" Authority for 
        // single-tenant apps, but it requires a custom IssuerValidator as shown 
        // in the comments below. 
        oidcOptions.Authority = $"{azureAdSettings.Instance.TrimEnd('/')}/{azureAdSettings.TenantId}/v2.0/";
        oidcOptions.ClientId = azureAdSettings.ClientId;
        oidcOptions.ClientSecret = azureAdSettings.ClientSecret;
        oidcOptions.ResponseType = OpenIdConnectResponseType.Code;

        // Many OIDC servers use "name" and "role" rather than the SOAP/WS-Fed 
        // defaults in ClaimTypes. If you don't use ClaimTypes, mapping inbound 
        // claims to ASP.NET Core's ClaimTypes isn't necessary.
        oidcOptions.MapInboundClaims = false;
        oidcOptions.TokenValidationParameters.NameClaimType = JwtRegisteredClaimNames.Name;
        oidcOptions.TokenValidationParameters.RoleClaimType = "role";

        // Many OIDC providers work with the default issuer validator, but the
        // configuration must account for the issuer parameterized with "{TENANT ID}" 
        // returned by the "common" endpoint's /.well-known/openid-configuration
        // For more information, see
        // https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/issues/1731

        //var microsoftIssuerValidator = AadIssuerValidator.GetAadIssuerValidator(oidcOptions.Authority);
        //oidcOptions.TokenValidationParameters.IssuerValidator = microsoftIssuerValidator.Validate;

        oidcOptions.Events = new OpenIdConnectEvents()
        {
            OnRedirectToIdentityProvider = context =>
            {
                // Build the external URL using the forwarded scheme, host and path base.
                // This ensures the RedirectUri uses the browser-visible domain (e.g. https://web.example.com)
                
                var request = context.HttpContext.Request;

                string selectedScheme = !context.ProtocolMessage.RedirectUri.Contains("localhost") ? "https" : request.Scheme;

                var externalRedirectUri = 
                    $"{selectedScheme}://{request.Host.ToString().TrimEnd('/')}" +
                    $"{request.PathBase.ToString().TrimEnd('/')}" +
                    $"/signin-oidc";

                context.ProtocolMessage.RedirectUri = externalRedirectUri;
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var authorizationClient = context.HttpContext.RequestServices.GetRequiredService<IAuthorizationApiClient>();

                if (!string.IsNullOrWhiteSpace(context.SecurityToken.RawData) && context.Principal.Identity is ClaimsIdentity identity && !identity.HasClaim(c => c.Type == "access_token"))
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

// This attaches a cookie OnValidatePrincipal callback to get a new access token when the current one expires, and
// reissue a cookie with the new access token saved inside. If the refresh fails, the user will be signed out.
builder.Services.ConfigureOidcRefreshHandling(CookieAuthenticationDefaults.AuthenticationScheme, "MicrosoftOidc");
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    //.AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddScoped<AuthenticationStateProvider, PersistingAuthenticationStateProvider>();
builder.Services.AddHttpForwarderWithServiceDiscovery();
builder.Services.AddSingleton<IUserIdProvider, SignalRCustomUserIdProvider>();

builder.Services.AddMudServices();

if (builder.Environment.IsDevelopment())
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
        var claims = new[]
        {
            new Claim("name", user.Identity?.Name ?? "unknown"),
            new Claim("preferred_username", user.FindFirstValue("preferred_username") ?? "unknown"),
            new Claim("access_token", user.FindFirstValue("access_token") ?? "unknown"),
            new Claim("sub", user.FindFirstValue("sub") ?? "unknown"),
        };

        return claims;
    };
});
}

builder.Services.AddSingleton<DynamicComponentResolver>();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024 * 20; // 200MB
});

var serviceBusConnectionString = builder.Configuration.GetConnectionString("sbus");
serviceBusConnectionString = serviceBusConnectionString?.Replace("https://", "sb://").Replace(":443/", "/");

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    
    // FanOut consumers are the only use for this in the Web Docgen project.
    x.AddFanOutConsumersForNonWorkerNode();
    x.UsingAzureServiceBus((context, cfg) =>
    {
        cfg.Host(serviceBusConnectionString, configure: config =>
        {
            config.TokenCredential = credentialHelper.GetAzureCredential();
        });

        cfg.AddFanOutSubscriptionEndpointsForNonWorkerNode(context);

    });
});

builder.Services.AddSingleton<IHostedService, ShutdownCleanupService>();

var redisConnection = builder.Services.BuildServiceProvider().GetRequiredService<IConnectionMultiplexer>();

builder.Services.AddDataProtection()
    .PersistKeysToStackExchangeRedis(redisConnection, "DataProtection-Keys");

var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};

app.UseForwardedHeaders(forwardedHeadersOptions);

// Configure the HTTP request pipeline.
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
    //.AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Microsoft.Greenlight.Web.DocGen.Client._Imports).Assembly);

// BFF Forwarders for the client.
// Use MapForwarder to forward requests to the API. API address is https://api-main, and all paths starting with /api will be forwarded.

// This does not require authorization.
app.MapForwarder("/api/configuration/frontend", "https://api-main/");

app.MapForwarder("/api/{**catch-all}", "https://api-main/", transformBuilder =>
{
    transformBuilder.AddRequestTransform(async transformContext =>
    {
        var accessToken = await transformContext.HttpContext.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(accessToken))
        {
            transformContext.ProxyRequest.Headers.Authorization = new("Bearer", accessToken);
        }
    });
}).WithMetadata(new RequestSizeLimitAttribute(512 * 1024 * 1024)) // 512MB
  .RequireAuthorization();

app.MapForwarder("/hubs/{**catch-all}", "https://api-main/", transformBuilder =>
{
    transformBuilder.AddRequestTransform(async transformContext =>
    {
        var accessToken = await transformContext.HttpContext.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(accessToken))
        {
            transformContext.ProxyRequest.Headers.Authorization = new("Bearer", accessToken);
        }
    });
}).RequireAuthorization();

app.MapGet("/api-address", () =>
{
    var apiAddress = builder.Configuration["services:api-main:https:0"];
    if (string.IsNullOrEmpty(apiAddress))
    {
        return Results.NotFound();
    }
    return Results.Ok(apiAddress.TrimEnd('/'));
});

app.MapGet("/configuration/token", async context =>
{
    // get access token from the request
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
