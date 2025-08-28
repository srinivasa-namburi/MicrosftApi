using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Greenlight.Web.DocGen.Client.Auth;
using Microsoft.Greenlight.Web.DocGen.Client.Components;
using Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;
using Microsoft.Greenlight.Web.DocGen.Client.Services;
using Microsoft.Greenlight.Web.Shared;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// IMPORTANT: Add the root components so the client app actually renders.
// The element ID "#app" must exist in the host page.
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();
builder.Services.AddSingleton<DynamicComponentResolver>();

builder.Services.AddMudServices();
var serverBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);

builder.Services.AddHttpClient("Microsoft.Greenlight.Web.DocGen.ServerAPI", client => client.BaseAddress = serverBaseAddress)
    .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Microsoft.Greenlight.Web.DocGen.ServerAPI"));

builder.Services.AddApiAuthorization();

builder.Services.AddHttpClient<IConfigurationApiClient, ConfigurationApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IAuthorizationApiClient, AuthorizationApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IChatApiClient, ChatApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IContentNodeApiClient, ContentNodeApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IDocumentGenerationApiClient, Microsoft.Greenlight.Web.Shared.ServiceClients.DocumentGenerationApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IDocumentIngestionApiClient, DocumentIngestionApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IDocumentProcessApiClient, DocumentProcessApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IDocumentOutlineApiClient, DocumentOutlineApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IReviewApiClient, ReviewApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.MaxRequestContentBufferSize = 10 * 1024 * 1024 * 20; // 200 MB
    return handler;
});

builder.Services.AddHttpClient<IPluginApiClient, PluginApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IDocumentLibraryApiClient, DocumentLibraryApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IFileApiClient, FileApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    handler.MaxRequestContentBufferSize = 10 * 1024 * 1024 * 20; // 200 MB
    return handler;
});

builder.Services.AddHttpClient<IDomainGroupsApiClient, DomainGroupsApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IContentReferenceApiClient, ContentReferenceApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IDocumentValidationApiClient, DocumentValidationApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IDocumentReindexApiClient, DocumentReindexApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IVectorStoreApiClient, VectorStoreApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IDefinitionsApiClient, DefinitionsApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

// Service used to aid in constructing editors
builder.Services.AddScoped<ValidationEditorService>();

// Shared SignalR connection provider
builder.Services.AddSingleton<SignalRConnectionService>();

// Factory for robust subscription management
builder.Services.AddSingleton<SignalRSubscriptionFactory>();

await builder.Build().RunAsync();
