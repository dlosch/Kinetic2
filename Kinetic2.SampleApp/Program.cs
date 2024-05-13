using Kinetic2;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.RegisterKinetic2();

builder.Services.AddLogging();

// add the polly resilience pipeline
builder.Services.AddResiliencePipeline("NotifyAsyncFromClass", builder => {
    builder
        .AddRetry(new RetryStrategyOptions() { BackoffType = DelayBackoffType.Exponential, MaxRetryAttempts = 6, UseJitter = true })
        .AddConcurrencyLimiter(1, 5)
        .AddTimeout(TimeSpan.FromSeconds(10));
});

builder.Services.AddResiliencePipeline("NotifyAsyncFromInterface", builder => {
    builder
        .AddRetry(new RetryStrategyOptions() { BackoffType = DelayBackoffType.Exponential, MaxRetryAttempts = 1, UseJitter = true })
        .AddConcurrencyLimiter(1, 5)
        .AddTimeout(TimeSpan.FromSeconds(10));
});

// register the notification service with some dummy stuff
builder.Services.Configure<NotificationOptions>(options => { options.NumFailures = 2; });
builder.Services.AddTransient<INotificationService, NotificationService>();

builder.Services.AddHostedService<BackgroundWorker>();

var app = builder.Build();

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", ([FromServices] INotificationService service) => {
    service.NotifyAsync("test@example.com", "Foo", "Bar");

    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary) {
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

internal class NotificationOptions {
    public int NumFailures { get; set; } = 3;
}

internal interface INotificationService {
    [ResiliencePipeline("NotifyAsyncFromInterface")]
    ValueTask NotifyAsync(string to, string subject, string body);
}

internal sealed class NotificationService : INotificationService {

    public NotificationService(IOptions<NotificationOptions> options, ILogger<NotificationService> logger) {
        _options = options;
        _logger = logger;
    }

    static int _counter = 0;
    private readonly IOptions<NotificationOptions> _options;
    private readonly ILogger<NotificationService> _logger;

    [ResiliencePipeline("NotifyAsyncFromClass")]
    public async ValueTask NotifyAsync(string to, string subject, string body) {
        _logger.LogWarning("Current Counter: {counter}", _counter);
        if (_counter++ % 2 == 0) {
            _logger.LogWarning("Raise error with new counter value {counter}", _counter);
            throw new Exception("Simulated exception");
        }

        // some logic here
        await Task.Delay(1000);
        _logger.LogWarning("Exit, everything fine.");
    }
}

internal sealed class BackgroundWorker : BackgroundService {
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundWorker> _logger;

    public BackgroundWorker(IServiceProvider serviceProvider, ILogger<BackgroundWorker> logger) {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    protected async override Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogWarning("Entering worker loop.");
        while (!stoppingToken.IsCancellationRequested) {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            try {
                _logger.LogWarning("Calling notificationService.NotifyAsync ...");
                await notificationService.NotifyAsync("test@example.com", "This is a test", "Message Body");
                _logger.LogWarning("Call to notificationService.NotifyAsync completed without error.");
                break;
            }
            catch (Exception xcptn) {
                _logger.LogError(xcptn, "Call to notificationService.NotifyAsync failed persistently");
            }
        }
        _logger.LogWarning("Exiting worker loop.");
    }
}
