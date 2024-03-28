using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Web.Shared.ServiceClients;

namespace ProjectVico.V2.Web.DocGen.ServiceClients;

internal sealed class ServerWeatherForecaster : BaseServiceClient<ServerWeatherForecaster>, IWeatherForecaster
{
    public ServerWeatherForecaster(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, ILogger<ServerWeatherForecaster> logger) 
        : base(httpClient, httpContextAccessor, logger)
    {
    }

    public async Task<IEnumerable<WeatherForecast>> GetWeatherForecastAsync()
    {
        var response = await SendGetRequestMessage("/api/weather");
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<WeatherForecast[]>()! ??
               throw new IOException("No weather forecast!");
    }

    
}