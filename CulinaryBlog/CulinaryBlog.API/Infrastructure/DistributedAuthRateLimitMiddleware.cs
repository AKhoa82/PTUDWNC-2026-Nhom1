using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace CulinaryBlog.API.Infrastructure;

public sealed record RateLimitCounterResult(long Count, TimeSpan RetryAfter);

public interface IAuthRateLimitCounter
{
    Task<RateLimitCounterResult> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken);
}

public sealed class RedisAuthRateLimitCounter : IAuthRateLimitCounter
{
    private const string IncrementScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then redis.call('PEXPIRE', KEYS[1], ARGV[1]) end
        return { count, redis.call('PTTL', KEYS[1]) }
        """;

    private readonly IDatabase _database;

    public RedisAuthRateLimitCounter(IConnectionMultiplexer connection)
    {
        _database = connection.GetDatabase();
    }

    public async Task<RateLimitCounterResult> IncrementAsync(
        string key,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var evaluation = await _database.ScriptEvaluateAsync(
            IncrementScript,
            [key],
            [(long)window.TotalMilliseconds]).ConfigureAwait(false);
        var result = (RedisResult[]?)evaluation;
        if (result is not { Length: >= 2 })
            throw new RedisException("Redis returned an invalid rate limit counter result.");

        var count = (long)result[0];
        var remainingMilliseconds = (long)result[1];
        return new RateLimitCounterResult(
            count,
            TimeSpan.FromMilliseconds(Math.Max(1, remainingMilliseconds)));
    }
}

public sealed class DistributedAuthRateLimitMiddleware
{
    private const int PermitLimit = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private readonly RequestDelegate _next;
    private readonly ILogger<DistributedAuthRateLimitMiddleware> _logger;

    public DistributedAuthRateLimitMiddleware(
        RequestDelegate next,
        ILogger<DistributedAuthRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method) ||
            !context.Request.Path.Equals("/api/v1/auth/google", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var ipAddress = context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown";
        var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ipAddress))).ToLowerInvariant();
        var key = $"culinaryblog:ratelimit:auth-google:v1:{keyHash}";

        RateLimitCounterResult counterResult;
        try
        {
            var counter = context.RequestServices.GetRequiredService<IAuthRateLimitCounter>();
            counterResult = await counter.IncrementAsync(key, Window, context.RequestAborted);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            _logger.LogError(exception, "Redis unavailable while checking Google login rate limit.");
            await Results.Problem(
                title: "Rate limit service unavailable",
                detail: "Đăng nhập Google tạm thời không khả dụng. Vui lòng thử lại sau.",
                statusCode: StatusCodes.Status503ServiceUnavailable)
                .ExecuteAsync(context);
            return;
        }

        if (counterResult.Count <= PermitLimit)
        {
            await _next(context);
            return;
        }

        var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(counterResult.RetryAfter.TotalSeconds));
        context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        await Results.Problem(
            title: "Too Many Requests",
            detail: "Quá giới hạn yêu cầu đăng nhập. Vui lòng thử lại sau.",
            statusCode: StatusCodes.Status429TooManyRequests)
            .ExecuteAsync(context);
    }
}
