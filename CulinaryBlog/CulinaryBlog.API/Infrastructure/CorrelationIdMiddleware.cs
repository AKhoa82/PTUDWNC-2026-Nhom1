using System.Diagnostics;
using System.Security.Claims;
using CulinaryBlog.Application.Common.Telemetry;
using Serilog.Context;

namespace CulinaryBlog.API.Infrastructure;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        var requestPath = context.Request.Path.Value ?? "/";
        var httpMethod = context.Request.Method;
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        Activity.Current?.SetTag("correlation.id", correlationId);
        Activity.Current?.SetTag("http.request.path", requestPath);

        var stopwatch = Stopwatch.StartNew();

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", requestPath))
        using (LogContext.PushProperty("TraceId", traceId))
        {
            try
            {
                await _next(context);
                stopwatch.Stop();

                var userId = GetAuthenticatedUserId(context);
                if (userId is not null)
                {
                    Activity.Current?.SetTag("enduser.id", userId);
                }

                using (LogContext.PushProperty("UserId", userId))
                {
                    var statusCode = context.Response.StatusCode;
                    var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

                    RecordMetrics(httpMethod, requestPath, statusCode, elapsedMs);

                    if (stopwatch.ElapsedMilliseconds > 500)
                    {
                        _logger.LogWarning(
                            "HTTP {HttpMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds} ms (Performance Alert > 500ms) [CorrelationId: {CorrelationId}, UserId: {UserId}, TraceId: {TraceId}]",
                            httpMethod,
                            requestPath,
                            statusCode,
                            stopwatch.ElapsedMilliseconds,
                            correlationId,
                            userId ?? "anonymous",
                            traceId);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "HTTP {HttpMethod} {RequestPath} responded {StatusCode} in {ElapsedMilliseconds} ms [CorrelationId: {CorrelationId}, UserId: {UserId}, TraceId: {TraceId}]",
                            httpMethod,
                            requestPath,
                            statusCode,
                            stopwatch.ElapsedMilliseconds,
                            correlationId,
                            userId ?? "anonymous",
                            traceId);
                    }
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var userId = GetAuthenticatedUserId(context);
                var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

                RecordMetrics(httpMethod, requestPath, StatusCodes.Status500InternalServerError, elapsedMs, isUnhandledException: true);

                using (LogContext.PushProperty("UserId", userId))
                {
                    _logger.LogError(
                        ex,
                        "HTTP {HttpMethod} {RequestPath} failed after {ElapsedMilliseconds} ms [CorrelationId: {CorrelationId}, UserId: {UserId}, TraceId: {TraceId}]",
                        httpMethod,
                        requestPath,
                        stopwatch.ElapsedMilliseconds,
                        correlationId,
                        userId ?? "anonymous",
                        traceId);
                }

                throw;
            }
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            var incoming = values.FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(incoming) && incoming.Length <= 128)
            {
                return incoming;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private static string? GetAuthenticatedUserId(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            return context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value;
        }

        return null;
    }

    private static void RecordMetrics(
        string httpMethod,
        string requestPath,
        int statusCode,
        double elapsedMs,
        bool isUnhandledException = false)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("http.request.method", httpMethod),
            new("url.path", requestPath),
            new("http.response.status_code", statusCode)
        };

        CulinaryBlogTelemetry.HttpRequestCounter.Add(1, tags);
        CulinaryBlogTelemetry.HttpRequestDurationHistogram.Record(elapsedMs, tags);

        if (isUnhandledException || statusCode >= 400)
        {
            CulinaryBlogTelemetry.HttpRequestErrorCounter.Add(1, tags);
        }
    }
}