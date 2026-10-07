using CulinaryBlog.Application.Features.Recipes.Commands.Steps;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Domain.Exceptions;


namespace CulinaryBlog.API.Endpoints;

public static class RecipeStepEndpoints
{
    public static void MapRecipeStepEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recipes/{id}/steps")
            .RequireAuthorization("AuthorPolicy");

        group.MapPost("/", async (Guid id, [FromBody] AddRecipeStepCommand command, System.Security.Claims.ClaimsPrincipal user, IMediator mediator, RecipeImageCacheInvalidator cacheStore, CancellationToken ct) =>
        {
                command.RecipeId = id;
                if (user?.Identity?.IsAuthenticated == true)
                {
                    command.AuthorId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    command.IsAdmin = user.IsInRole("Admin");
                }
                var result = await mediator.Send(command, ct);
                // Retrieve slug for cache invalidation
            await cacheStore.InvalidateAsync(cancellationToken: ct);
                return Results.Created($"/api/v1/recipes/{id}/steps/{result.Id}", result);
        })
        .WithName("AddRecipeStep")
        .WithSummary("Thêm bước thực hiện")
        .Produces<CulinaryBlog.Application.DTOs.RecipeStepDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPut("/{stepId}", async (Guid id, Guid stepId, [FromBody] UpdateRecipeStepCommand command, System.Security.Claims.ClaimsPrincipal user, IMediator mediator, RecipeImageCacheInvalidator cacheStore, CancellationToken ct) =>
        {
            command.RecipeId = id;
            command.StepId = stepId;
            if (user?.Identity?.IsAuthenticated == true)
            {
                command.AuthorId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                command.IsAdmin = user.IsInRole("Admin");
            }
            var result = await mediator.Send(command, ct);
            await cacheStore.InvalidateAsync(cancellationToken: ct);
            return Results.Ok(result);
        })
        .WithName("UpdateRecipeStep")
        .WithSummary("Cập nhật bước thực hiện")
        .Produces<CulinaryBlog.Application.DTOs.RecipeStepDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{stepId}", async (Guid id, Guid stepId, System.Security.Claims.ClaimsPrincipal user, IMediator mediator, RecipeImageCacheInvalidator cacheStore, CancellationToken ct) =>
        {
            var command = new DeleteRecipeStepCommand { RecipeId = id, StepId = stepId };
            if (user?.Identity?.IsAuthenticated == true)
            {
                command.AuthorId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                command.IsAdmin = user.IsInRole("Admin");
            }
            await mediator.Send(command, ct);
            await cacheStore.InvalidateAsync(cancellationToken: ct);
            return Results.NoContent();
        })
        .WithName("DeleteRecipeStep")
        .WithSummary("Xóa bước thực hiện")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
