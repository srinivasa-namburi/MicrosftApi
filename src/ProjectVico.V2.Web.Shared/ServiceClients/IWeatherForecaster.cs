using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Web.Shared.ServiceClients;

public interface IWeatherForecaster : IServiceClient
{
    Task<IEnumerable<WeatherForecast>> GetWeatherForecastAsync();
}