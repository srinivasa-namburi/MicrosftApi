using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ProjectVico.V2.Web.DocGen.Components;
using MudBlazor.Services;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Web.DocGen.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using ProjectVico.V2.Web.DocGen.ServiceClients;
using ProjectVico.V2.Web.Shared.ServiceClients;
using Microsoft.AspNetCore.SignalR;
using ProjectVico.V2.Shared.Helpers;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var azureAdSection = builder.Configuration.GetSection("AzureAd");
builder.Services.Configure<AzureAdOptions>(azureAdSection);

var azureAdSettings = azureAdSection.Get<AzureAdOptions>();


var serviceConfigurationSection = builder.Configuration.GetSection("ServiceConfiguration");
builder.Services.Configure<ServiceConfigurationOptions>(serviceConfigurationSection);

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
        oidcOptions.Authority = $"https://login.microsoftonline.com/{azureAdSettings.TenantId}/v2.0/";
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
                // When we're hosting outside localhost, we need to rewrite to use https
                // This is because we're behind a reverse proxy that terminates SSL and renders the URLs internally as http, not https
                if (!context.ProtocolMessage.RedirectUri.Contains("localhost"))
                {
                    var urlBuilder = new UriBuilder(context.ProtocolMessage.RedirectUri)
                    {
                        Scheme = "https",
                        Port = -1
                    };
                    context.ProtocolMessage.RedirectUri = urlBuilder.Uri.ToString();
                }
                return Task.FromResult(0);
            }
        };
    })
    .AddCookie("Cookies");

// This attaches a cookie OnValidatePrincipal callback to get a new access token when the current one expires, and
// reissue a cookie with the new access token saved inside. If the refresh fails, the user will be signed out.
builder.Services.ConfigureCookieOidcRefresh("Cookies", "MicrosoftOidc");
builder.Services.AddAuthorization();
builder.Services.AddScoped<AuthenticationStateProvider, PersistingAuthenticationStateProvider>();

builder.Services.AddHttpForwarderWithServiceDiscovery();
builder.Services.AddHttpContextAccessor();

builder.AddAzureBlobService("blob-docing");
builder.Services.AddScoped<AzureFileHelper>();

builder.Services.AddHttpClient<IWeatherForecaster, ServerWeatherForecaster>(httpClient =>
{
    httpClient.BaseAddress = new("https://api-main");
});
builder.Services.AddHttpClient<IDocumentGenerationApiClient, DocumentGenerationApiClient>(httpClient =>
{
    httpClient.BaseAddress = new("https://api-main");
});
builder.Services.AddHttpClient<IDocumentIngestionApiClient, DocumentIngestionApiClient>(httpClient =>
{
    httpClient.BaseAddress = new("https://api-main");
});
builder.Services.AddHttpClient<IContentNodeApiClient, ContentNodeApiClient>(httpClient =>
{
    httpClient.BaseAddress = new("https://api-main");
});
builder.Services.AddHttpClient<IChatApiClient, ChatApiClient>(httpClient =>
{
    httpClient.BaseAddress = new("https://api-main");
});


// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

builder.Services.AddSingleton<IUserIdProvider, SignalRCustomUserIdProvider>();

builder.Services.AddSignalR();

var app = builder.Build();



// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    //app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapDefaultEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGroup("/authentication").MapLoginAndLogout();

app.Run();
