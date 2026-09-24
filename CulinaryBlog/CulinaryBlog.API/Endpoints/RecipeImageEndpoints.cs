using System.Security.Claims;
using System.Threading.RateLimiting;
using CulinaryBlog.Application.Common.Helpers;
using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Recipes.Images;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Minio.Exceptions;

namespace CulinaryBlog.API.Endpoints;

public static class RecipeImageEndpoints
{
    private const long MultipartLimit = FileValidationHelper.MaxFileSizeBytes + 64 * 1024;

    public static IServiceCollection AddRecipeImageFeature(this IServiceCollection services)
    {
        services.AddScoped<RecipeImageService>();
        services.AddScoped<RecipeImageCacheInvalidator>();
        services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = MultipartLimit);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)) : 60;
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
            options.AddPolicy("RecipeImageUpload", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });
        return services;
    }

    public static void UseRecipeImageRequestLimits(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsPost(context.Request.Method) &&
                context.Request.Path.StartsWithSegments("/api/v1/recipes") &&
                context.Request.Path.Value?.TrimEnd('/').EndsWith("/images", StringComparison.OrdinalIgnoreCase) == true)
            {
                var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                if (feature is { IsReadOnly: false }) feature.MaxRequestBodySize = MultipartLimit;
                if (context.Request.ContentLength > MultipartLimit)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    return;
                }
            }
            await next(context);
        });
    }

    public static RouteGroupBuilder MapRecipeImageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recipes/{id:guid}/images")
            .WithTags("Recipe Images").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (ArgumentException ex) { return Results.BadRequest(new { message = ex.Message }); }
            catch (RecipeImageValidationException ex)
            {
                return Results.Problem(statusCode: 422, title: "Dữ liệu ảnh không hợp lệ.", detail: ex.Message);
            }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is MinioException or HttpRequestException or FileOperationUnavailableException ||
                (ex is OperationCanceledException && !context.HttpContext.RequestAborted.IsCancellationRequested))
            {
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RecipeImageEndpoints").LogError(ex, "Recipe image storage operation failed");
                return Results.Problem(statusCode: 503, title: "Dịch vụ lưu trữ tạm thời không khả dụng.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RecipeImageEndpoints").LogError(ex, "Recipe image operation failed");
                return Results.Problem(statusCode: 500, title: "Không thể hoàn tất thao tác ảnh.");
            }
        });

        group.MapPost("/", async (Guid id, IFormFile file,
            [FromForm] string? altText, [FromForm] bool? isPrimary,
            ClaimsPrincipal user, RecipeImageService images, RecipeImageCacheInvalidator cache, CancellationToken ct) =>
        {
            var result = await images.UploadAsync(id, file, altText, isPrimary == true,
                UserId(user), user.IsInRole("Admin"), ct);
            await cache.InvalidateAsync();
            return Results.Created($"/api/v1/recipes/{id}/images/{result.ImageId}", result);
        }).DisableAntiforgery().RequireRateLimiting("RecipeImageUpload").WithName("UploadRecipeImageV1");

        group.MapPatch("/{imageId:guid}", async (Guid id, Guid imageId,
            UpdateRecipeImageRequest request, ClaimsPrincipal user, RecipeImageService images,
            RecipeImageCacheInvalidator cache, CancellationToken ct) =>
        {
            var result = await images.UpdateAsync(id, imageId, request,
                UserId(user), user.IsInRole("Admin"), ct);
            await cache.InvalidateAsync();
            return Results.Ok(result);
        }).WithName("UpdateRecipeImageV1");

        group.MapDelete("/{imageId:guid}", async (Guid id, Guid imageId,
            ClaimsPrincipal user, RecipeImageService images, RecipeImageCacheInvalidator cache, CancellationToken ct) =>
        {
            await images.DeleteAsync(id, imageId, UserId(user), user.IsInRole("Admin"), ct);
            await cache.InvalidateAsync();
            return Results.NoContent();
        }).WithName("DeleteRecipeImageV1");
        return group;
    }

    private static string UserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Token không có user ID.");
}
