using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using ProjectVico.V2.Web.DocGen.Client.Auth;
using ProjectVico.V2.Web.DocGen.Client.ServiceClients;
using ProjectVico.V2.Web.Shared;
using ProjectVico.V2.Web.Shared.ServiceClients;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();
builder.Services.AddSingleton<DynamicComponentResolver>();

builder.Services.AddMudServices();
var serverBaseAddress = new Uri(builder.HostEnvironment.BaseAddress);

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

builder.Services.AddHttpClient<IDocumentGenerationApiClient, DocumentGenerationApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});

builder.Services.AddHttpClient<IDocumentIngestionApiClient, DocumentIngestionApiClient>(client =>
{
    client.BaseAddress = serverBaseAddress;
});



await builder.Build().RunAsync();