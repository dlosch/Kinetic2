using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kinetic2;

public static class ResilienceExtensions {

    public static async ValueTask<TRes> ExecuteResiliencePipeline<TType, TRes>(IServiceProvider serviceProvider, string pipelineName, Func<CancellationToken, ValueTask<TRes>> invoker, CancellationToken cancellationToken = default) where TType : class {
        var @pip = default(Polly.ResiliencePipeline);
        var @log = default(Microsoft.Extensions.Logging.ILogger<TType>);
        if (serviceProvider is { }) {
            var @p = serviceProvider.GetRequiredService<Polly.Registry.ResiliencePipelineProvider<string>>();
            @pip = @p.GetPipeline(pipelineName);
            @log = serviceProvider.GetService<ILogger<TType>>();
        }

        try {
            if (@pip is { }) {
                @log?.LogInformation("Executing resilience pipeline {resiliencePipelineName}", pipelineName);
                return await @pip.ExecuteAsync(async (@tokenName) => await invoker(@tokenName), cancellationToken);

            }
            else {
                @log?.LogWarning("Failed to resolve resilience pipeline with name '{resiliencePipelineName}'. Executing without resilience.", "NotifyAsync");
                return await invoker(cancellationToken);
            }
        }
        catch (System.Exception xcptn) {
            @log?.LogError(xcptn, "An unfortunate error occurred");
            throw;
        }
    }

    public static async ValueTask<TRes> ExecuteResiliencePipeline<TType, TRes>(IServiceProvider serviceProvider, string pipelineName, Func<CancellationToken, Task<TRes>> invoker, CancellationToken cancellationToken = default) where TType : class {
        var @pip = default(Polly.ResiliencePipeline);
        var @log = default(Microsoft.Extensions.Logging.ILogger<TType>);
        if (serviceProvider is { }) {
            var @p = serviceProvider.GetRequiredService<Polly.Registry.ResiliencePipelineProvider<string>>();
            @pip = @p.GetPipeline(pipelineName);
            @log = serviceProvider.GetService<ILogger<TType>>();
        }

        try {
            if (@pip is { }) {
                @log?.LogInformation("Executing resilience pipeline {resiliencePipelineName}", pipelineName);
                return await @pip.ExecuteAsync(async (@tokenName) => await invoker(@tokenName), cancellationToken);

            }
            else {
                @log?.LogWarning("Failed to resolve resilience pipeline with name '{resiliencePipelineName}'. Executing without resilience.", "NotifyAsync");
                return await invoker(cancellationToken);
            }
        }
        catch (System.Exception xcptn) {
            @log?.LogError(xcptn, "An unfortunate error occurred");
            throw;
        }
    }

    public static async ValueTask ExecuteResiliencePipeline<TType>(IServiceProvider serviceProvider, string pipelineName, Func<CancellationToken, ValueTask> invoker, CancellationToken cancellationToken = default) where TType : class {
        var @pip = default(Polly.ResiliencePipeline);
        var @log = default(Microsoft.Extensions.Logging.ILogger<TType>);
        if (serviceProvider is { }) {
            var @p = serviceProvider.GetRequiredService<Polly.Registry.ResiliencePipelineProvider<string>>();
            @pip = @p.GetPipeline(pipelineName);
            @log = serviceProvider.GetService<ILogger<TType>>();
        }

        try {
            if (@pip is { }) {
                @log?.LogInformation("Executing resilience pipeline {resiliencePipelineName}", pipelineName);
                await @pip.ExecuteAsync(async (@tokenName) => await invoker(@tokenName), cancellationToken);

            }
            else {
                @log?.LogWarning("Failed to resolve resilience pipeline with name '{resiliencePipelineName}'. Executing without resilience.", "NotifyAsync");
                await invoker(cancellationToken);
            }
        }
        catch (System.Exception xcptn) {
            @log?.LogError(xcptn, "An unfortunate error occurred");
            throw;
        }
    }

    public static async ValueTask ExecuteResiliencePipeline<TType>(IServiceProvider serviceProvider, string pipelineName, Func<CancellationToken, Task> invoker, CancellationToken cancellationToken = default) where TType : class {
        var @pip = default(Polly.ResiliencePipeline);
        var @log = default(Microsoft.Extensions.Logging.ILogger<TType>);
        if (serviceProvider is { }) {
            var @p = serviceProvider.GetRequiredService<Polly.Registry.ResiliencePipelineProvider<string>>();
            @pip = @p.GetPipeline(pipelineName);
            @log = serviceProvider.GetService<ILogger<TType>>();
        }

        try {
            if (@pip is { }) {
                @log?.LogInformation("Executing resilience pipeline {resiliencePipelineName}", pipelineName);
                await @pip.ExecuteAsync(async (@tokenName) => await invoker(@tokenName), cancellationToken);

            }
            else {
                @log?.LogWarning("Failed to resolve resilience pipeline with name '{resiliencePipelineName}'. Executing without resilience.", "NotifyAsync");
                await invoker(cancellationToken);
            }
        }
        catch (System.Exception xcptn) {
            @log?.LogError(xcptn, "An unfortunate error occurred");
            throw;
        }
    }

}
