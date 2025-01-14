namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents a weather forecast.
/// </summary>
public sealed class WeatherForecast
{
    /// <summary>
    /// Date of the weather forecast.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Temperature in Celsius.
    /// </summary>
    public int TemperatureC { get; set; }

    /// <summary>
    /// Summary of the weather forecast.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Gets the temperature in Fahrenheit.
    /// </summary>
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
