using System.Security.Claims;
using System.Threading.RateLimiting;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.Features.Files;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Minio.Exceptions;

namespace CulinaryBlog.API.Endpoints;

public static class FileEndpoints
{
    public static IServiceCollection AddFileEndpoints(this IServiceCollection services)
    {
        services.AddScoped<FileManagementService>();
        services.AddAuthorization(options => options.AddPolicy("FileUser", policy =>
            policy.RequireAuthenticatedUser().RequireAssertion(context =>
                Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty)));
        services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = FileValidationHelper.MaxFileSizeBytes);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("FileUpload", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                }));
        });
        return services;
    }

    public static void UseFileRequestLimits(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/v1/files") && HttpMethods.IsPost(context.Request.Method))
            {
                const long requestLimit = FileValidationHelper.MaxFileSizeBytes + 64 * 1024;
                var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = requestLimit;
                if (context.Request.ContentLength > requestLimit)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return;
                }
            }
            await next(context);
        });
    }

    public static RouteGroupBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/files").WithTags("Files").RequireAuthorization("FileUser");
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is MinioException or HttpRequestException ||
                (ex is OperationCanceledException && !context.HttpContext.RequestAborted.IsCancellationRequested))
            {
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("FileEndpoints").LogError(ex, "File storage operation failed");
                return Results.Problem(statusCode: 503, title: "Dịch vụ lưu trữ tạm thời không khả dụng.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("FileEndpoints").LogError(ex, "File operation failed");
                return Results.Problem(statusCode: 500, title: "Không thể hoàn tất thao tác tệp tin.");
            }
        });

        group.MapPost("/upload", async (IFormFile file, string? folder, ClaimsPrincipal user,
            FileManagementService files, CancellationToken ct) =>
        {
            var result = await files.UploadAsync(file, folder, UserId(user), ct);
            return Results.Created(result.Url, result);
        }).WithName("UploadFileV1").RequireRateLimiting("FileUpload")
          // JWT bearer only; no cookie authentication is accepted by this application.
          .DisableAntiforgery();

        group.MapDelete("/", async (string fileUrl, ClaimsPrincipal user, FileManagementService files, CancellationToken ct) =>
        {
            await files.DeleteAsync(fileUrl, UserId(user), ct);
            return Results.NoContent();
        }).WithName("DeleteFileV1");

        group.MapGet("/exists", async (string fileUrl, ClaimsPrincipal user, FileManagementService files, CancellationToken ct) =>
            Results.Ok(new { fileUrl, exists = await files.ExistsAsync(fileUrl, UserId(user), ct) }))
            .WithName("CheckFileExistsV1");
        return group;
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
