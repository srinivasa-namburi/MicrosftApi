using ProjectVico.V2.Plugins.Earthquake.Connectors;
using ProjectVico.V2.Plugins.Earthquake.NativePlugins;
using ProjectVico.V2.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IEarthquakeConnector, USGSEarthquakeConnector>();
builder.Services.AddScoped<EarthquakePlugin>();

builder.AddPluggableEndpointDefinitions();

var app = builder.Build();

app.MapPluggableEndpointDefinitions();
app.MapDefaultEndpoints();
app.UseSwagger();
app.UseSwaggerUI();

app.Run();