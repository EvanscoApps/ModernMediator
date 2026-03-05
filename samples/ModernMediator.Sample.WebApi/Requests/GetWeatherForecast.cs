using ModernMediator;
using ModernMediator.AspNetCore;

namespace ModernMediator.Sample.WebApi.Requests;

// ── Basic request/response with [Endpoint] GET ───────────────

[Endpoint("/api/weather", "GET")]
public record GetWeatherForecast : IRequest<WeatherForecast[]>;

public record WeatherForecast(DateOnly Date, int TemperatureC, string Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public class GetWeatherForecastHandler : IRequestHandler<GetWeatherForecast, WeatherForecast[]>
{
    private static readonly string[] Summaries =
        ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

    public Task<WeatherForecast[]> Handle(GetWeatherForecast request, CancellationToken cancellationToken = default)
    {
        var forecasts = Enumerable.Range(1, 5).Select(index =>
            new WeatherForecast(
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                Summaries[Random.Shared.Next(Summaries.Length)]))
            .ToArray();

        return Task.FromResult(forecasts);
    }
}
