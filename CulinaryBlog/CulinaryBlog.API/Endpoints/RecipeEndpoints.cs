using CulinaryBlog.Application.DTOs;
using CulinaryBlog.Application.Features.Recipes.Commands.PublishRecipe;
using CulinaryBlog.Application.Features.Recipes.Commands.UnpublishRecipe;
using CulinaryBlog.Application.Features.Recipes.Commands.ArchiveRecipe;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

using CulinaryBlog.Domain.Exceptions;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Recipes.Commands.DeleteRecipe;

namespace CulinaryBlog.API.Endpoints;

public static class RecipeEndpoints
{
    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder app)
    {
            var group = app.MapGroup("/api/v1/recipes");

        // PATCH /api/v1/recipes/{id}/publish - Xuất bản công thức
        group.MapPatch("/{id:guid}/publish", async (
            Guid id,
            ISender sender,
            ClaimsPrincipal user,
            RecipeImageCacheInvalidator cacheStore,
            CancellationToken ct) =>
        {
                var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                var isAdmin = user.IsInRole("Admin");
                var result = await sender.Send(new PublishRecipeCommand(id, currentUserId, isAdmin), ct);
                
                await cacheStore.InvalidateAsync(cancellationToken: ct);

                return Results.Ok(result);
        })
        .WithName("PublishRecipe")
        .WithSummary("Xuất bản công thức nấu ăn")
        .RequireAuthorization("AuthorPolicy")
        .Produces<RecipeDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // PATCH /api/v1/recipes/{id}/unpublish - Hủy xuất bản công thức
        group.MapPatch("/{id:guid}/unpublish", async (
            Guid id,
            ISender sender,
            ClaimsPrincipal user,
            RecipeImageCacheInvalidator cacheStore,
            CancellationToken ct) =>
        {
                var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                var isAdmin = user.IsInRole("Admin");
                var result = await sender.Send(new UnpublishRecipeCommand(id, currentUserId, isAdmin), ct);
                
                await cacheStore.InvalidateAsync(cancellationToken: ct);

                return Results.Ok(result);
        })
        .WithName("UnpublishRecipe")
        .WithSummary("Hủy xuất bản công thức nấu ăn")
        .RequireAuthorization("AuthorPolicy")
        .Produces<RecipeDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // PATCH /api/v1/recipes/{id}/archive - Lưu trữ công thức
        group.MapPatch("/{id:guid}/archive", async (
            Guid id,
            ISender sender,
            ClaimsPrincipal user,
            RecipeImageCacheInvalidator cacheStore,
            CancellationToken ct) =>
        {
                var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                var isAdmin = user.IsInRole("Admin");
                var result = await sender.Send(new ArchiveRecipeCommand(id, currentUserId, isAdmin), ct);
                
                await cacheStore.InvalidateAsync(cancellationToken: ct);

                return Results.Ok(result);
        })
        .WithName("ArchiveRecipe")
        .WithSummary("Lưu trữ công thức nấu ăn")
        .RequireAuthorization("AuthorPolicy")
        .Produces<RecipeDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ISender sender,
            ClaimsPrincipal user,
            
            RecipeImageCacheInvalidator cacheStore,
            CancellationToken cancellationToken) =>
        {
            var currentUserId =
                user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                throw new UnauthorizedAccessException("Token không có user ID.");
            }

            var isAdmin = user.IsInRole("Admin");

            await sender.Send(
                new DeleteRecipeCommand(
                    id,
                    currentUserId,
                    isAdmin),
                cancellationToken);

            await cacheStore.InvalidateAsync(cancellationToken: cancellationToken);



            return Results.NoContent();
        })
        .WithName("DeleteRecipe")
        .WithSummary("FR-RCP-007 - Xóa công thức")
        .RequireAuthorization("AuthorPolicy")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}