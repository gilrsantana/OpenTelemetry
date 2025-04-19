using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var resource = ResourceBuilder
    .CreateDefault()
    .AddService("app-service", "1.0.0");
var activitySource = new ActivitySource("app-activity-source", "1.0.0");
var meter = new Meter("app-meter", "1.0.0");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(rb => rb
        .AddService("app-service", "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(inst =>
        {
            inst.RecordException = true;
        })
        .AddHttpClientInstrumentation()
        .AddSource("app-activity-source")
        .AddOtlpExporter(options => options.Endpoint = new Uri("http://localhost:4317")))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(
            "Microsoft.AspNetCore.hosting",
            "Microsoft.AspNetCore.Server.Kestrel",
            "System.Net.http",
            "System.Net.NameResolution",
            "System.Threading.Tasks",
            "app-meter")
        .AddOtlpExporter(otplOptions =>
        {
            otplOptions.Endpoint = new Uri("http://localhost:4317");
        }));

builder.Logging
    .AddOpenTelemetry(options =>
    {
        options.IncludeScopes = true;
        options.ParseStateValues = true;
        options.IncludeFormattedMessage = true;
        options.SetResourceBuilder(resource);
        options.AddOtlpExporter(otplOptions =>
        {
            otplOptions.Endpoint = new Uri("http://localhost:4317");
        });
        options.AddConsoleExporter();
    });


var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.DarkMode = true;
    options.DotNetFlag = true;
    options.Theme = ScalarTheme.None;
    options.DefaultOpenAllTags = true;
    options.Title = "OpenTelemetry API";
    options.ShowSidebar = false;
});

app.UseHttpsRedirection();
app.UseRouting();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapPost("/setweather" , ()  =>
    {
        var forecast = new WeatherForecast(DateOnly.FromDateTime(DateTime.Now), 32, summaries[5]);
        return Results.Created("", forecast);
    })
    .WithName("SetWeather");

app.MapPost("/counter", (int count) =>
{
    var m = meter.CreateCounter<int>("contador", description: "Contador de requisições na API");
    m.Add(count > 0 ? count : 1);
    return m;
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
