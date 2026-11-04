using System.Diagnostics;
using CulinaryBlog.Application.Common.Telemetry;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        using var activity = CulinaryBlogTelemetry.ActivitySource.StartActivity($"MediatR.{requestName}");
        activity?.SetTag("mediatr.request_name", requestName);

        var traceId = Activity.Current?.TraceId.ToString();

        _logger.LogInformation(
            "Handling {RequestName} (TraceId: {TraceId})",
            requestName,
            traceId);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();

            stopwatch.Stop();
            activity?.SetTag("mediatr.elapsed_ms", stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > 500)
            {
                _logger.LogWarning(
                    "Performance Alert - Long Running Request: {RequestName} ({ElapsedMilliseconds} ms, TraceId: {TraceId})",
                    requestName,
                    stopwatch.ElapsedMilliseconds,
                    traceId);
            }
            else
            {
                _logger.LogInformation(
                    "Handled {RequestName} ({ElapsedMilliseconds} ms, TraceId: {TraceId})",
                    requestName,
                    stopwatch.ElapsedMilliseconds,
                    traceId);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _logger.LogError(
                ex,
                "Request Failure: {RequestName} ({ElapsedMilliseconds} ms, TraceId: {TraceId}) - {ErrorMessage}",
                requestName,
                stopwatch.ElapsedMilliseconds,
                traceId,
                ex.Message);
            throw;
        }
    }
}